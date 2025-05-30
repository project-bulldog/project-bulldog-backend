using backend.Services.Interfaces;

namespace backend.Services.Implementations;

public class FakeNotificationService : INotificationService
{
    private readonly ILogger<FakeNotificationService> _logger;

    public FakeNotificationService(ILogger<FakeNotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendReminderAsync(Guid userId, string subject, string message)
    {
        _logger.LogInformation("📨 [FAKE NOTIFICATION] To: {UserId} | Subject: {Subject} | Message: {Message}", userId, subject, message);
        return Task.CompletedTask;
    }
}
