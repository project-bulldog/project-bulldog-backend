using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using backend.Data;
using backend.Enums;
using backend.Helpers;
using backend.Models;
using backend.Services.Auth.Interfaces;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace backend.Services.Auth.Implementations
{
    public class TwoFactorService : ITwoFactorService
    {
        private readonly BulldogDbContext _context;
        private readonly ILogger<TwoFactorService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAmazonSimpleEmailService _sesService;
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _fromNumber;
        private readonly bool _useTwilio;

        public TwoFactorService(
            BulldogDbContext context,
            ILogger<TwoFactorService> logger,
            IConfiguration configuration,
            IAmazonSimpleEmailService sesService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _sesService = sesService;

            _accountSid = _configuration["Twilio:AccountSid"] ?? "";
            _authToken = _configuration["Twilio:AuthToken"] ?? "";
            _fromNumber = _configuration["Twilio:FromNumber"] ?? "";

            _useTwilio = !string.IsNullOrEmpty(_accountSid) &&
                         _accountSid != "your_twilio_account_sid_here" &&
                         !string.IsNullOrEmpty(_authToken) &&
                         !string.IsNullOrEmpty(_fromNumber);

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
            var method = !string.IsNullOrWhiteSpace(user.PhoneNumber) ? OtpDeliveryMethod.Sms : OtpDeliveryMethod.Email;
            return await GenerateAndSendOtpAsync(user, method);
        }

        public async Task<string> GenerateAndSendOtpAsync(User user, OtpDeliveryMethod method)
        {
            var code = GenerateOtpCode();

            user.CurrentOtp = code;
            user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
            user.OtpAttemptsLeft = 5;

            await _context.SaveChangesAsync();

            bool sent = false;

            if (method == OtpDeliveryMethod.Sms && !string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                sent = await SendOtpSmsAsync(user.PhoneNumber, code);
                if (sent)
                {
                    var sanitizedPhoneNumber = LogSanitizer.SanitizeForLog(user.PhoneNumber);
                    _logger.LogInformation("OTP sent via SMS to {PhoneNumber}", sanitizedPhoneNumber);
                }
                else
                {
                    var sanitizedPhoneNumber = LogSanitizer.SanitizeForLog(user.PhoneNumber);
                    _logger.LogWarning("SMS failed for {PhoneNumber}, trying email fallback", sanitizedPhoneNumber);
                }
            }

            if (!sent && (method == OtpDeliveryMethod.Email || (method == OtpDeliveryMethod.Sms && !sent)))
            {
                await SendOtpEmailAsync(user.Email, code);
                _logger.LogInformation("OTP sent via email.");
                sent = true;
            }

            if (!sent)
            {
                _logger.LogError("Failed to send OTP via any method for user {UserId}", user.Id);
                throw new InvalidOperationException("Failed to send verification code via SMS or Email.");
            }

            return code;
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

            //Passed
            user.CurrentOtp = null;
            user.OtpExpiresAt = null;
            user.OtpAttemptsLeft = 5;
            user.PhoneNumberVerified = true;

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            if (_useTwilio)
            {
                try
                {
                    var messageResource = await MessageResource.CreateAsync(
                        body: message,
                        from: new PhoneNumber(_fromNumber),
                        to: new PhoneNumber(phoneNumber)
                    );

                    if (messageResource.Status == MessageResource.StatusEnum.Failed ||
                        messageResource.Status == MessageResource.StatusEnum.Undelivered)
                    {
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    var sanitizedPhoneNumber = LogSanitizer.SanitizeForLog(phoneNumber);
                    _logger.LogError(ex, "Failed to send SMS to {PhoneNumber}", sanitizedPhoneNumber);
                    return false;
                }
            }
            else
            {
                var sanitizedPhoneNumber = LogSanitizer.SanitizeForLog(phoneNumber);
                var sanitizedMessage = LogSanitizer.SanitizeForLog(message);
                _logger.LogInformation("📱 [FAKE SMS] To: {PhoneNumber} | Message: {Message}", sanitizedPhoneNumber, sanitizedMessage);
                await Task.Delay(100);
                return true;
            }
        }

        private async Task<bool> SendOtpSmsAsync(string phoneNumber, string otpCode)
        {
            var message = $"Your verification code is: {otpCode}. This code will expire in 5 minutes.";
            return await SendSmsAsync(phoneNumber, message);
        }

        private async Task SendOtpEmailAsync(string email, string otpCode)
        {
            var fromEmail = _configuration["AWS:FromEmail"];
            if (string.IsNullOrEmpty(fromEmail))
            {
                throw new InvalidOperationException("AWS:FromEmail is not configured.");
            }

            var subject = "Your Bulldog Verification Code";
            var body = $"Your verification code is: {otpCode}. This code will expire in 5 minutes.";

            var request = new SendEmailRequest
            {
                Source = fromEmail,
                Destination = new Destination { ToAddresses = [email] },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Text = new Content(body) }
                }
            };

            try
            {
                await _sesService.SendEmailAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AWS SES failed to send OTP email to [REDACTED]");
                throw;
            }
        }

        private string GenerateOtpCode()
        {
            var rng = new Random();
            return rng.Next(100000, 999999).ToString();
        }
    }
}
