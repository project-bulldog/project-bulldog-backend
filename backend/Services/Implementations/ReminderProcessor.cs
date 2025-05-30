using backend.Data;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;
public class ReminderProcessor : IReminderProcessor
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<ReminderProcessor> _logger;

    public ReminderProcessor(BulldogDbContext context, ILogger<ReminderProcessor> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ProcessDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var dueReminders = await _context.Reminders
            .Include(r => r.ActionItem)
            .Where(r => !r.IsSent && r.ReminderTime <= now)
            .ToListAsync(cancellationToken);

        foreach (var reminder in dueReminders)
        {
            _logger.LogInformation("⏰ Reminder triggered! Message: {Message}, ActionItem: {ActionItemText}, User: {UserId}",
                reminder.Message,
                reminder.ActionItem?.Text,
                reminder.UserId);

            reminder.IsSent = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("✅ Processed {Count} due reminders at {Time}", dueReminders.Count, DateTime.UtcNow);
    }
}
