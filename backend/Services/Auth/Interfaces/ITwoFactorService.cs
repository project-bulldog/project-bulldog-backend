using backend.Models;

namespace backend.Services.Auth.Interfaces;

public interface ITwoFactorService
{
    Task<string> GenerateAndSendOtpAsync(User user);
    Task<bool> VerifyOtpAsync(User user, string code);
}
