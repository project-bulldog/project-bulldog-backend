using backend.Models;

namespace backend.Mappers;

public static class ReminderMapper
{
    public static ReminderDto ToDto(Reminder reminder)
    {
        return new ReminderDto
        {
            Id = reminder.Id,
            Message = reminder.Message,
            ReminderTime = reminder.ReminderTime,
            IsSent = reminder.IsSent,
            SentAt = reminder.SentAt,
            ActionItemId = reminder.ActionItemId,
            IsActive = reminder.IsActive,
            IsMissed = reminder.IsMissed,
            SnoozedUntil = reminder.SnoozedUntil
        };
    }
}
