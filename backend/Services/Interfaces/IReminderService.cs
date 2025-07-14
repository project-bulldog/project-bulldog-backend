using backend.Dtos.Reminders;

namespace backend.Services.Interfaces;

public interface IReminderService
{
    Task<IEnumerable<ReminderDto>> GetRemindersAsync();
    Task<ReminderDto?> GetReminderAsync(Guid id);
    Task<ReminderDto> CreateReminderAsync(CreateReminderDto dto);
    Task<bool> UpdateReminderAsync(Guid id, UpdateReminderDto dto);
    Task<bool> DeleteReminderAsync(Guid id);
    Task<bool> SnoozeReminderAsync(Guid reminderId, int snoozeMinutes);
    Task<IEnumerable<ReminderDto>> GetMissedRemindersAsync();
}
