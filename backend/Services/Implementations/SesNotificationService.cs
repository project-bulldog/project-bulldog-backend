using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using backend.Data;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public partial class SesNotificationService : INotificationService
{
    private readonly ILogger<SesNotificationService> _logger;
    private readonly IConfiguration _config;
    private readonly IAmazonSimpleEmailService _ses;
    private readonly BulldogDbContext _db;

    public SesNotificationService(
        ILogger<SesNotificationService> logger,
        IConfiguration config,
        IAmazonSimpleEmailService ses,
        BulldogDbContext db)
    {
        _logger = logger;
        _config = config;
        _ses = ses;
        _db = db;
    }

    public async Task SendReminderAsync(Guid userId, string subject, string message)
    {
        var toEmail = await GetEmailByUserIdAsync(userId);
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("❌ Could not find email for user ID {UserId}", userId);
            return;
        }

        await SendEmailAsync(toEmail, subject, message);
    }

    public async Task SendSecurityAlertAsync(string subject, string message)
    {
        var toEmail = _config["Security:AlertEmail"];
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("❌ Security alert email not configured.");
            return;
        }

        await SendEmailAsync(toEmail, subject, message);
    }

    public async Task SendOtpEmailAsync(string email, string subject, string message)
    {
        await SendEmailAsync(email, subject, message);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var fromEmail = _config["AWS:FromEmail"];

        var sendRequest = new SendEmailRequest
        {
            Source = fromEmail,
            Destination = new Destination { ToAddresses = new List<string> { toEmail } },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body
                {
                    Html = new Content(htmlBody),
                    Text = new Content(MyRegex().Replace(htmlBody, ""))
                }
            }
        };

        var response = await _ses.SendEmailAsync(sendRequest);
        _logger.LogInformation("✅ Email successfully sent | SES MessageId: {MessageId}", response.MessageId);
    }

    private async Task<string?> GetEmailByUserIdAsync(Guid userId)
    {
        return await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();
    }

    [System.Text.RegularExpressions.GeneratedRegex("<.*?>")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}
