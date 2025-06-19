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
        {
            throw new InvalidOperationException("User does not have a phone number.");
        }

        var code = GenerateOtpCode();

        user.CurrentOtp = code;
        user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(5); // 🔒 short-lived

        await _context.SaveChangesAsync();

        // TODO: Replace this with actual SMS service
        _logger.LogInformation("Sending 2FA code {Code} to {Phone}", code, user.PhoneNumber);

        return code; // Return for debugging/testing now
    }

    public async Task<bool> VerifyOtpAsync(User user, string code)
    {
        if (string.IsNullOrWhiteSpace(user.CurrentOtp) || user.OtpExpiresAt == null)
            return false;

        if (DateTime.UtcNow > user.OtpExpiresAt.Value)
            return false;

        if (!string.Equals(user.CurrentOtp, code, StringComparison.Ordinal))
            return false;

        // ✅ Passed: clear the OTP after verification
        user.CurrentOtp = null;
        user.OtpExpiresAt = null;

        await _context.SaveChangesAsync();

        return true;
    }

    private string GenerateOtpCode()
    {
        var rng = new Random();
        return rng.Next(100000, 999999).ToString();
    }
}

