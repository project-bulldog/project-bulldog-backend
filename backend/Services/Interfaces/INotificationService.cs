namespace backend.Services.Interfaces;

public interface INotificationService
{
    Task SendReminderAsync(Guid userId, string subject, string message);
    Task SendSecurityAlertAsync(string subject, string message);
}
