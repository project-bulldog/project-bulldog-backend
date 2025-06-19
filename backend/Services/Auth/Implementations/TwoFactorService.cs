using backend.Data;
using backend.Models;
using backend.Services.Auth.Interfaces;

namespace backend.Services.Auth.Implementations;

public class TwoFactorService : ITwoFactorService
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<TwoFactorService> _logger;
    // Optionally: inject ITwilioService or similar later

    public TwoFactorService(BulldogDbContext context, ILogger<TwoFactorService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GenerateAndSendOtpAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            throw new InvalidOperationException("User does not have a phone number.");

        var code = GenerateOtpCode();

        user.CurrentOtp = code;
        user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5);
        user.OtpAttemptsLeft = 5; // reset attempts on resend

        await _context.SaveChangesAsync();

        // TODO: Replace with actual SMS service
        _logger.LogInformation("Sending OTP {Code} to {Phone}", code, user.PhoneNumber);

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

    private string GenerateOtpCode()
    {
        var rng = new Random();
        return rng.Next(100000, 999999).ToString();
    }
}

