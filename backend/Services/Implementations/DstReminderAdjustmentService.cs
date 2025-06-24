using backend.Data;
using backend.Helpers;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

/// <summary>
/// Background service that periodically checks for DST changes and adjusts reminder times
/// to ensure they fire at the correct wall time, even when DST rules change.
/// </summary>
public class DstReminderAdjustmentService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DstReminderAdjustmentService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6); // Check every 6 hours

    public DstReminderAdjustmentService(IServiceProvider services, ILogger<DstReminderAdjustmentService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🕐 DST Reminder Adjustment Service started. Checking every {Interval} hours", _checkInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndAdjustRemindersForDstAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in DST reminder adjustment service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    #region Private Methods
    private async Task CheckAndAdjustRemindersForDstAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BulldogDbContext>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        var now = DateTime.UtcNow;

        var futureReminders = await context.Reminders
            .Include(r => r.ActionItem)
            .Where(r => !r.IsSent &&
                        r.ReminderTime > now &&
                        r.ReminderTime <= now.AddDays(7) &&
                        r.ActionItem != null &&
                        r.ActionItem.DueAt.HasValue &&
                        r.ActionItem.ReminderMinutesBeforeDue.HasValue)
            .ToListAsync(cancellationToken);

        if (futureReminders.Count == 0)
        {
            _logger.LogDebug("No future reminders to check for DST adjustments");
            return;
        }

        _logger.LogInformation("🔍 Checking {Count} future reminders for DST adjustments", futureReminders.Count);

        var adjustedCount = 0;
        foreach (var reminder in futureReminders)
        {
            try
            {
                if (await TryAdjustReminderAsync(reminder, userService))
                {
                    adjustedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check DST adjustment for reminder {ReminderId}", reminder.Id);
            }
        }

        if (adjustedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("✅ Adjusted {Count} reminders for DST changes", adjustedCount);
        }
        else
        {
            _logger.LogDebug("No reminders needed DST adjustment");
        }
    }

    private async Task<bool> TryAdjustReminderAsync(Reminder reminder, IUserService userService)
    {
        var user = await userService.GetUserEntityAsync(reminder.UserId);
        if (user?.TimeZoneId == null)
        {
            _logger.LogDebug("⏭️ Skipping reminder {ReminderId} — user has no TimeZoneId", reminder.Id);
            return false;
        }

        var tzId = TimeZoneHelpers.NormalizeTimeZoneId(user.TimeZoneId);
        var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);

        var originalDueTimeLocal = TimeZoneInfo.ConvertTimeFromUtc(reminder.ActionItem!.DueAt!.Value, userTimeZone);
        var expectedReminderTimeLocal = originalDueTimeLocal.AddMinutes(-reminder.ActionItem.ReminderMinutesBeforeDue!.Value);
        var expectedReminderTimeUtc = TimeZoneInfo.ConvertTimeToUtc(expectedReminderTimeLocal, userTimeZone);

        var timeDifference = Math.Abs((expectedReminderTimeUtc - reminder.ReminderTime).TotalMinutes);

        if (timeDifference > 1)
        {
            var oldTime = reminder.ReminderTime;
            reminder.ReminderTime = expectedReminderTimeUtc;

            _logger.LogInformation("🔄 DST adjustment for reminder {ReminderId}: {OldTime} UTC → {NewTime} UTC (diff: {Diff} min)",
                reminder.Id, oldTime, expectedReminderTimeUtc, timeDifference);

            return true;
        }

        return false;
    }
    #endregion
}
