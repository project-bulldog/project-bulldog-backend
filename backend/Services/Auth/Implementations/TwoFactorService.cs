using backend.Data;
using backend.Models;
using backend.Services.Auth.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace backend.Services.Auth.Implementations;

public class TwoFactorService : ITwoFactorService
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<TwoFactorService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;
    private readonly bool _useTwilio;

    public TwoFactorService(BulldogDbContext context, ILogger<TwoFactorService> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;

        _accountSid = _configuration["Twilio:AccountSid"] ?? "";
        _authToken = _configuration["Twilio:AuthToken"] ?? "";
        _fromNumber = _configuration["Twilio:FromNumber"] ?? "";

        // Debug logging to see what's configured
        _logger.LogInformation("Twilio Configuration Check:");
        _logger.LogInformation("AccountSid: {AccountSidPresent}", !string.IsNullOrEmpty(_accountSid) ? "Present" : "Missing");
        _logger.LogInformation("AuthToken: {AuthTokenPresent}", !string.IsNullOrEmpty(_authToken) ? "Present" : "Missing");
        _logger.LogInformation("FromNumber: {FromNumberPresent}", !string.IsNullOrEmpty(_fromNumber) ? "Present" : "Missing");

        _useTwilio = !string.IsNullOrEmpty(_accountSid) &&
                     _accountSid != "your_twilio_account_sid_here" &&
                     !string.IsNullOrEmpty(_authToken) &&
                     !string.IsNullOrEmpty(_fromNumber);

        _logger.LogInformation("Use Twilio: {UseTwilio}", _useTwilio);

        if (_useTwilio)
        {
            TwilioClient.Init(_accountSid, _authToken);
            _logger.LogInformation("Twilio SMS service initialized");
        }
        else
        {
            _logger.LogInformation("Using fake SMS service for development");
        }
    }

    public async Task<string> GenerateAndSendOtpAsync(User user)
    {
        // Default to SMS if available, otherwise email
        var method = !string.IsNullOrWhiteSpace(user.PhoneNumber) ? "sms" : "email";
        return await GenerateAndSendOtpAsync(user, method);
    }

    public async Task<string> GenerateAndSendOtpAsync(User user, string method)
    {
        var code = GenerateOtpCode();

        user.CurrentOtp = code;
        user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
        user.OtpAttemptsLeft = 5; // reset attempts on resend

        await _context.SaveChangesAsync();

        bool sent = false;

        if (method.ToLower() == "sms" && !string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            sent = await SendOtpAsync(user.PhoneNumber, code);
            if (sent)
            {
                _logger.LogInformation("OTP sent successfully via SMS to {PhoneNumber}", user.PhoneNumber);
            }
            else
            {
                _logger.LogWarning("SMS failed for {PhoneNumber}, trying email fallback", user.PhoneNumber);
            }
        }

        if (!sent && method.ToLower() == "email" || (method.ToLower() == "sms" && !sent))
        {
            sent = await SendOtpEmailAsync(user.Email, code);
            if (sent)
            {
                _logger.LogInformation("OTP sent via email to {Email}", user.Email);
            }
        }

        if (!sent)
        {
            _logger.LogError("Failed to send OTP via any method for user {UserId}", user.Id);
            throw new InvalidOperationException("Failed to send verification code. Please try again.");
        }

        return code; // For testing/debug only — remove in prod
    }

    public async Task<bool> VerifyOtpAsync(User user, string code)
    {
        if (user.OtpAttemptsLeft <= 0)
        {
            _logger.LogWarning("User {UserId} exceeded OTP attempts", user.Id);
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.CurrentOtp) || user.OtpExpiresAt == null)
            return false;

        if (DateTime.UtcNow > user.OtpExpiresAt.Value)
            return false;

        if (!string.Equals(user.CurrentOtp, code, StringComparison.Ordinal))
        {
            user.OtpAttemptsLeft--;
            await _context.SaveChangesAsync();
            return false;
        }

        // ✅ Passed
        user.CurrentOtp = null;
        user.OtpExpiresAt = null;
        user.OtpAttemptsLeft = 5;
        user.PhoneNumberVerified = true;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        if (_useTwilio)
        {
            try
            {
                _logger.LogInformation("Sending SMS to {PhoneNumber}", phoneNumber);

                var messageResource = await MessageResource.CreateAsync(
                    body: message,
                    from: new PhoneNumber(_fromNumber),
                    to: new PhoneNumber(phoneNumber)
                );

                _logger.LogInformation("SMS sent successfully. SID: {MessageSid}, Status: {Status}",
                    messageResource.Sid, messageResource.Status);

                // Check if the message was actually delivered or blocked
                if (messageResource.Status == MessageResource.StatusEnum.Failed ||
                    messageResource.Status == MessageResource.StatusEnum.Undelivered)
                {
                    _logger.LogWarning("SMS delivery failed. Status: {Status}, Error: {ErrorCode}",
                        messageResource.Status, messageResource.ErrorCode);
                    return false;
                }

                // For trial accounts, even "sent" status might mean blocked
                // We'll assume it's successful for now, but log the status
                if (messageResource.Status == MessageResource.StatusEnum.Sent)
                {
                    _logger.LogInformation("SMS appears to be sent successfully");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", phoneNumber);
                return false;
            }
        }
        else
        {
            // Fake SMS service for development
            _logger.LogInformation("📱 [FAKE SMS] To: {PhoneNumber} | Message: {Message}", phoneNumber, message);
            await Task.Delay(100); // Simulate network delay
            return true;
        }
    }

    public async Task<bool> SendOtpAsync(string phoneNumber, string otpCode)
    {
        var message = $"Your verification code is: {otpCode}. This code will expire in 5 minutes.";
        return await SendSmsAsync(phoneNumber, message);
    }

    public async Task<bool> SendOtpEmailAsync(string email, string otpCode)
    {
        // For development, just log the email
        _logger.LogInformation("📧 [FAKE EMAIL] To: {Email} | OTP Code: {OtpCode}", email, otpCode);
        await Task.Delay(100); // Simulate network delay
        return true;
    }

    private string GenerateOtpCode()
    {
        var rng = new Random();
        return rng.Next(100000, 999999).ToString();
    }
}

