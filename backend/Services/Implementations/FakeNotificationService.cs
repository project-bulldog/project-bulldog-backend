using backend.Helpers;
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
        var sanitizedSubject = LogSanitizer.SanitizeForLog(subject);
        var sanitizedMessage = LogSanitizer.SanitizeForLog(message);
        _logger.LogInformation("📨 [FAKE NOTIFICATION] To: {UserId} | Subject: {Subject} | Message: {Message}", userId, sanitizedSubject, sanitizedMessage);
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

        var sanitizedSubject = LogSanitizer.SanitizeForLog(subject);
        var sanitizedMessage = LogSanitizer.SanitizeForLog(message);
        _logger.LogInformation("📨 [FAKE NOTIFICATION] To: [REDACTED EMAIL] | Subject: {Subject} | Message: {Message}", sanitizedSubject, sanitizedMessage);
        return Task.CompletedTask;
    }
    #endregion

    #region OTP Notifications
    public async Task SendOtpEmailAsync(string email, string subject, string message)
    {
        var sanitizedSubject = LogSanitizer.SanitizeForLog(subject);
        var sanitizedMessage = LogSanitizer.SanitizeForLog(message);
        _logger.LogInformation("📨 [FAKE NOTIFICATION] To: [REDACTED] | Subject: {Subject} | Message: {Message}", sanitizedSubject, sanitizedMessage);
        await Task.CompletedTask;
    }
    #endregion
}
