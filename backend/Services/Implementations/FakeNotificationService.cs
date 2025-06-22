using backend.Services.Interfaces;

namespace backend.Services.Implementations;

public class FakeNotificationService : INotificationService
{
    private readonly ILogger<FakeNotificationService> _logger;
    private readonly IConfiguration _config;

    public FakeNotificationService(ILogger<FakeNotificationService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    #region User Notifications
    public Task SendReminderAsync(Guid userId, string subject, string message)
    {
        _logger.LogInformation("📨 [FAKE NOTIFICATION] To: {UserId} | Subject: {Subject} | Message: {Message}", userId, subject, message);
        return Task.CompletedTask;
    }
    #endregion

    #region Security Notifications
    public Task SendSecurityAlertAsync(string subject, string message)
    {
        var email = _config["Security:AlertEmail"];

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Security alert email not configured.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("📨 [FAKE NOTIFICATION] To: {Email} | Subject: {Subject} | Message: {Message}", email, subject, message);
        return Task.CompletedTask;
    }
    #endregion

    #region OTP Notifications
    public async Task SendOtpEmailAsync(string email, string subject, string message)
    {
        _logger.LogInformation("📨 [FAKE NOTIFICATION] To: {Email} | Subject: {Subject} | Message: {Message}", email, subject, message);
        await Task.CompletedTask;
    }
    #endregion
}
