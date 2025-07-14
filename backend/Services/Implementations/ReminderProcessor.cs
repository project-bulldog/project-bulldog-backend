using backend.Data;
using backend.Helpers;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class ReminderProcessor : IReminderProcessor
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<ReminderProcessor> _logger;
    private readonly INotificationService _notificationService;
    private readonly TelemetryClient _telemetryClient;
    private readonly IUserService _userService;
    private readonly int _cleanupDays;

    public ReminderProcessor(
        BulldogDbContext context,
        ILogger<ReminderProcessor> logger,
        INotificationService notificationService,
        TelemetryClient telemetryClient,
        IUserService userService,
        IConfiguration config)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
        _telemetryClient = telemetryClient;
        _userService = userService;

        _cleanupDays = config.GetValue<int>("ReminderCleanupDays", 7); // fallback to 7 days
    }

    public async Task ProcessDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dueReminders = await GetDueRemindersAsync(now, cancellationToken);

        await ProcessEachReminderAsync(dueReminders, now, cancellationToken);
        await CleanupMissedRemindersAsync(now, cancellationToken);
    }

    #region Private Methods

    private async Task<List<Reminder>> GetDueRemindersAsync(DateTime now, CancellationToken ct)
    {
        return await _context.Reminders
            .Include(r => r.ActionItem)
            .Where(r =>
                !r.IsSent &&
                r.IsActive &&
                (r.SnoozedUntil == null || r.SnoozedUntil <= now) &&
                r.ReminderTime <= now &&
                r.SendAttempts < r.MaxSendAttempts)
            .ToListAsync(ct);
    }

    private async Task ProcessEachReminderAsync(List<Reminder> reminders, DateTime now, CancellationToken ct)
    {
        int sentCount = 0;
        int skippedDueToMissingTask = 0;
        int skippedDueToInactive = 0;
        int skippedDueToDst = 0;
        int skippedDueToSnooze = 0;
        int markedMissed = 0;

        foreach (var reminder in reminders)
        {
            _logger.LogInformation("🔄 Processing reminder {ReminderId} for user {UserId}", reminder.Id, reminder.UserId);

            try
            {
                if (reminder.ActionItem == null)
                {
                    _logger.LogWarning("⚠️ Reminder {ReminderId} has no associated ActionItem (possibly deleted) — skipping", reminder.Id);
                    skippedDueToMissingTask++;
                    continue;
                }

                if (!reminder.IsActive)
                {
                    _logger.LogInformation("⏸ Reminder {ReminderId} is inactive — skipping", reminder.Id);
                    skippedDueToInactive++;
                    continue;
                }

                if (reminder.SnoozedUntil.HasValue && reminder.SnoozedUntil > now)
                {
                    _logger.LogInformation("😴 Reminder {ReminderId} is snoozed until {SnoozedUntil} — skipping", reminder.Id, reminder.SnoozedUntil);
                    skippedDueToSnooze++;
                    continue;
                }

                if (!reminder.IsSent && reminder.ReminderTime < now)
                {
                    reminder.IsMissed = true;
                    reminder.IsActive = false;
                    markedMissed++;

                    _logger.LogInformation("⏳ Reminder {ReminderId} is overdue and was not sent — marked as missed.", reminder.Id);
                    continue;
                }

                if (await ShouldRecalculateForDstAsync(reminder, now))
                {
                    _logger.LogInformation("⏰ DST recalculation triggered for reminder {ReminderId}, skipping processing", reminder.Id);
                    await RecalculateReminderForDstAsync(reminder, now);
                    skippedDueToDst++;
                    continue;
                }

                _logger.LogInformation("📤 Sending reminder {ReminderId} to user {UserId}", reminder.Id, reminder.UserId);
                await _notificationService.SendReminderAsync(
                    reminder.UserId,
                    "You have a reminder",
                    reminder.Message);

                reminder.IsSent = true;
                reminder.SentAt = DateTime.UtcNow;
                sentCount++;

                TrackReminderProcessed(reminder);
            }
            catch (Exception ex)
            {
                reminder.SendAttempts += 1;
                _logger.LogError(ex, "❌ Failed to send reminder notification to user {UserId}", reminder.UserId);

                if (reminder.SendAttempts >= reminder.MaxSendAttempts)
                {
                    _logger.LogWarning("🚫 Reminder {ReminderId} for user {UserId} reached max send attempts ({MaxSendAttempts}) and will no longer be retried.",
                        reminder.Id,
                        reminder.UserId,
                        reminder.MaxSendAttempts);
                }
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("✅ Reminder batch complete at {Time} — Sent: {Sent}, Missed: {Missed}, Skipped (Missing): {Missing}, Inactive: {Inactive}, DST: {Dst}, Snoozed: {Snoozed}",
            DateTime.UtcNow, sentCount, markedMissed, skippedDueToMissingTask, skippedDueToInactive, skippedDueToDst, skippedDueToSnooze);
    }

    private async Task CleanupMissedRemindersAsync(DateTime now, CancellationToken ct)
    {
        var cutoff = now.AddDays(-_cleanupDays);

        var toDelete = await _context.Reminders
            .Where(r => r.IsMissed && r.ReminderTime < cutoff)
            .ToListAsync(ct);

        if (toDelete.Count == 0) return;

        _context.Reminders.RemoveRange(toDelete);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("🧹 Cleaned up {Count} missed reminders older than {Cutoff} (cutoff = {Days} days)",
            toDelete.Count, cutoff, _cleanupDays);
    }

    private async Task<bool> ShouldRecalculateForDstAsync(Reminder reminder, DateTime now)
    {
        if (reminder.ActionItem?.DueAt == null || reminder.ActionItem?.ReminderMinutesBeforeDue == null)
            return false;

        try
        {
            var user = await _userService.GetUserEntityAsync(reminder.UserId);
            if (user?.TimeZoneId == null)
                return false;

            var tzId = TimeZoneHelpers.NormalizeTimeZoneId(user.TimeZoneId);

            var expectedReminderTimeUtc = TimeZoneHelpers.ConvertToUtc(
                TimeZoneHelpers.ConvertToLocal(reminder.ActionItem.DueAt.Value, tzId)
                    .AddMinutes(-reminder.ActionItem.ReminderMinutesBeforeDue.Value),
                tzId);

            var currentReminderTimeUtc = reminder.ReminderTime;

            var diff = Math.Abs((expectedReminderTimeUtc - currentReminderTimeUtc).TotalMinutes);
            _logger.LogInformation("🕐 DST Check - ReminderId: {ReminderId}, Expected: {Expected}, Current: {Current}, Diff: {Diff}",
                reminder.Id, expectedReminderTimeUtc, currentReminderTimeUtc, diff);

            if (diff > 1)
            {
                _logger.LogInformation("🕐 DST recalculation needed for reminder {ReminderId}", reminder.Id);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check DST recalculation for reminder {ReminderId}", reminder.Id);
            return false;
        }
    }

    private async Task RecalculateReminderForDstAsync(Reminder reminder, DateTime now)
    {
        try
        {
            var user = await _userService.GetUserEntityAsync(reminder.UserId);
            if (user?.TimeZoneId == null)
                return;

            var tzId = TimeZoneHelpers.NormalizeTimeZoneId(user.TimeZoneId);

            var newReminderTimeUtc = TimeZoneHelpers.ConvertToUtc(
                TimeZoneHelpers.ConvertToLocal(reminder.ActionItem!.DueAt!.Value, tzId)
                    .AddMinutes(-reminder.ActionItem.ReminderMinutesBeforeDue!.Value),
                tzId);

            if (newReminderTimeUtc != reminder.ReminderTime && newReminderTimeUtc > now)
            {
                _logger.LogInformation("🔄 DST recalculation for reminder {ReminderId}: {OldTime} UTC → {NewTime} UTC",
                    reminder.Id, reminder.ReminderTime, newReminderTimeUtc);

                reminder.ReminderTime = newReminderTimeUtc;
            }
            else if (newReminderTimeUtc <= now)
            {
                reminder.ReminderTime = now.AddSeconds(1);

                _logger.LogInformation("⏰ DST recalculation made reminder {ReminderId} immediately due (was {OldTime} UTC)",
                    reminder.Id, reminder.ReminderTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recalculate reminder {ReminderId} for DST", reminder.Id);
        }
    }

    private void TrackReminderProcessed(Reminder reminder)
    {
        if (reminder.SentAt.HasValue)
        {
            _telemetryClient.TrackEvent("ReminderProcessed", new Dictionary<string, string>
            {
                { "UserId", reminder.UserId.ToString() },
                { "ReminderId", reminder.Id.ToString() },
                { "Status", " ✅ Success" },
                { "Timestamp", reminder.SentAt.Value.ToString("o") }
            });

            _telemetryClient.GetMetric("RemindersSentPerDay", "UserId")
                .TrackValue(1, reminder.UserId.ToString());
        }
        else
        {
            _logger.LogWarning("Attempted to track reminder {ReminderId} with no SentAt value", reminder.Id);
        }
    }

    #endregion
}

