using backend.Data;
using backend.Helpers;
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

    public ReminderProcessor(
        BulldogDbContext context,
        ILogger<ReminderProcessor> logger,
        INotificationService notificationService,
        TelemetryClient telemetryClient,
        IUserService userService)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
        _telemetryClient = telemetryClient;
        _userService = userService;
    }

    public async Task ProcessDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueReminders = await _context.Reminders
            .Include(r => r.ActionItem)
            .Where(r => !r.IsSent && r.ReminderTime <= now && r.SendAttempts < r.MaxSendAttempts)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("🔍 Found {Count} due reminders to process", dueReminders.Count);

        foreach (var reminder in dueReminders)
        {
            _logger.LogInformation("🔄 Processing reminder {ReminderId} for user {UserId}", reminder.Id, reminder.UserId);
            try
            {
                if (await ShouldRecalculateForDstAsync(reminder, now))
                {
                    _logger.LogInformation("⏰ DST recalculation triggered for reminder {ReminderId}, skipping processing", reminder.Id);
                    await RecalculateReminderForDstAsync(reminder, now);
                    continue;
                }

                _logger.LogInformation("📤 Sending reminder {ReminderId} to user {UserId}", reminder.Id, reminder.UserId);
                await _notificationService.SendReminderAsync(
                    reminder.UserId,
                    "You have a reminder",
                    reminder.Message);

                reminder.IsSent = true;
                reminder.SentAt = DateTime.UtcNow;

                TrackReminderProcessed(reminder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to send reminder notification to user {UserId}", reminder.UserId);
                reminder.SendAttempts += 1;

                if (reminder.SendAttempts >= reminder.MaxSendAttempts)
                {
                    _logger.LogWarning("🚫 Reminder {ReminderId} for user {UserId} reached max send attempts ({MaxSendAttempts}) and will no longer be retried.",
                        reminder.Id,
                        reminder.UserId,
                        reminder.MaxSendAttempts);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("✅ Processed {Count} due reminders at {Time}", dueReminders.Count, DateTime.UtcNow);
    }

    private async Task<bool> ShouldRecalculateForDstAsync(Models.Reminder reminder, DateTime now)
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
                _logger.LogInformation("🕐 DST recalculation needed for reminder {ReminderId}: expected {Expected} UTC, current {Current} UTC (diff: {Diff} min)",
                    reminder.Id, expectedReminderTimeUtc, currentReminderTimeUtc, diff);
                return true;
            }
            _logger.LogInformation("🕐 No DST recalculation needed for reminder {ReminderId}", reminder.Id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check DST recalculation for reminder {ReminderId}", reminder.Id);
            return false;
        }
    }

    private async Task RecalculateReminderForDstAsync(Models.Reminder reminder, DateTime now)
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
                var oldTime = reminder.ReminderTime;
                reminder.ReminderTime = newReminderTimeUtc;

                _logger.LogInformation("🔄 DST recalculation for reminder {ReminderId}: {OldTime} UTC → {NewTime} UTC",
                    reminder.Id, oldTime, newReminderTimeUtc);
            }
            else if (newReminderTimeUtc <= now)
            {
                _logger.LogInformation("⏰ DST recalculation made reminder {ReminderId} immediately due (was {OldTime} UTC)",
                    reminder.Id, reminder.ReminderTime);
                reminder.ReminderTime = now.AddSeconds(1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recalculate reminder {ReminderId} for DST", reminder.Id);
        }
    }

    #region Private Methods
    private void TrackReminderProcessed(Models.Reminder reminder)
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
