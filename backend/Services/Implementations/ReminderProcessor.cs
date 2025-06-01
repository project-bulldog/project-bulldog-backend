using backend.Data;
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

    public ReminderProcessor(BulldogDbContext context, ILogger<ReminderProcessor> logger, INotificationService notificationService, TelemetryClient telemetryClient)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
        _telemetryClient = telemetryClient;
    }

    public async Task ProcessDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueReminders = await _context.Reminders
            .Include(r => r.ActionItem)
            .Where(r => !r.IsSent && r.ReminderTime <= now && r.SendAttempts < r.MaxSendAttempts)
            .ToListAsync(cancellationToken);

        foreach (var reminder in dueReminders)
        {
            try
            {
                // 🔔 Send fake notification
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
