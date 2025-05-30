namespace backend.Services.Interfaces;

public interface IReminderProcessor
{
    Task ProcessDueRemindersAsync(CancellationToken cancellationToken = default);
}
