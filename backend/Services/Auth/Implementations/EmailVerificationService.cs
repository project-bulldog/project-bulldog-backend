using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using backend.Data;
using backend.Models;
using backend.Services.Auth.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace backend.Services.Auth.Implementations;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<EmailVerificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAmazonSimpleEmailService _sesService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public EmailVerificationService(
        BulldogDbContext context,
        ILogger<EmailVerificationService> logger,
        IConfiguration configuration,
        IAmazonSimpleEmailService sesService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _sesService = sesService;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public async Task<string> GenerateAndSendVerificationEmailAsync(User user)
    {
        var token = GenerateVerificationToken(user);
        var verificationUrl = CreateVerificationUrlAsync(user, token);

        await SendVerificationEmailAsync(user.Email, verificationUrl);

        return token;
    }

    public async Task<bool> VerifyEmailTokenAsync(string token)
    {
        try
        {
            var protector = _dataProtectionProvider.CreateProtector("EmailVerification");
            var unprotectedToken = protector.Unprotect(token);

            var parts = unprotectedToken.Split('|');
            if (parts.Length != 3) return false;

            var userId = Guid.Parse(parts[0]);
            var email = parts[1];
            var expiryTicks = long.Parse(parts[2]);
            var expiry = new DateTime(expiryTicks, DateTimeKind.Utc);

            if (DateTime.UtcNow > expiry)
            {
                _logger.LogWarning("Email verification token expired for user {UserId}", userId);
                return false;
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null || user.Email != email)
            {
                _logger.LogWarning("Invalid email verification token for user {UserId}", userId);
                return false;
            }

            user.EmailVerified = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Email verified for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email token");
            return false;
        }
    }

    private string GenerateVerificationToken(User user)
    {
        var protector = _dataProtectionProvider.CreateProtector("EmailVerification");
        var expiry = DateTime.UtcNow.AddHours(24); // 24 hour expiry
        var tokenData = $"{user.Id}|{user.Email}|{expiry.Ticks}";
        return protector.Protect(tokenData);
    }

    private string CreateVerificationUrlAsync(User user, string token)
    {
        var frontendUrl = _configuration["Frontend:BaseUrl"];
        return $"{frontendUrl}/verify-email?token={Uri.EscapeDataString(token)}";
    }

    private async Task SendVerificationEmailAsync(string email, string verificationUrl)
    {
        var fromEmail = _configuration["AWS:FromEmail"];
        if (string.IsNullOrEmpty(fromEmail))
        {
            throw new InvalidOperationException("AWS:FromEmail is not configured.");
        }

        var subject = "Verify Your Bulldog Account";
        var htmlBody = CreateVerificationEmailHtml(verificationUrl);
        var textBody = CreateVerificationEmailText(verificationUrl);

        var request = new SendEmailRequest
        {
            Source = fromEmail,
            Destination = new Destination { ToAddresses = [email] },
            Message = new Message
            {
                Subject = new Content(subject),
                Body = new Body
                {
                    Html = new Content(htmlBody),
                    Text = new Content(textBody)
                }
            }
        };

        try
        {
            await _sesService.SendEmailAsync(request);
            _logger.LogInformation("Verification email sent to [REDACTED]");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AWS SES failed to send verification email to [REDACTED]");
            throw;
        }
    }

    private string CreateVerificationEmailHtml(string verificationUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Verify Your Bulldog Account</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='text-align: center; margin-bottom: 30px;'>
        <h1 style='color: #2563eb; margin-bottom: 10px;'>Welcome to Bulldog!</h1>
        <p style='font-size: 18px; color: #666;'>Please verify your email address to complete your registration.</p>
    </div>

    <div style='background-color: #f8fafc; border-radius: 8px; padding: 30px; margin-bottom: 30px;'>
        <p style='margin-bottom: 20px;'>Click the button below to verify your email address:</p>

        <div style='text-align: center;'>
            <a href='{verificationUrl}'
               style='display: inline-block; background-color: #2563eb; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;'>
                Verify Email Address
            </a>
        </div>

        <p style='margin-top: 20px; font-size: 14px; color: #666;'>
            If the button doesn't work, you can copy and paste this link into your browser:<br>
            <a href='{verificationUrl}' style='color: #2563eb; word-break: break-all;'>{verificationUrl}</a>
        </p>
    </div>

    <div style='text-align: center; color: #666; font-size: 14px;'>
        <p>This link will expire in 24 hours.</p>
        <p>If you didn't create a Bulldog account, you can safely ignore this email.</p>
    </div>
</body>
</html>";
    }

    private string CreateVerificationEmailText(string verificationUrl)
    {
        return $@"
Welcome to Bulldog!

Please verify your email address to complete your registration.

Click the link below to verify your email address:
{verificationUrl}

This link will expire in 24 hours.

If you didn't create a Bulldog account, you can safely ignore this email.";
    }
}
