using backend.Models;

namespace backend.Services.Auth.Interfaces;

public interface ITwoFactorService
{
    Task<string> GenerateAndSendOtpAsync(User user);
    Task<string> GenerateAndSendOtpAsync(User user, string method);
    Task<bool> VerifyOtpAsync(User user, string code);
    Task<bool> SendSmsAsync(string phoneNumber, string message);
    Task<bool> SendOtpAsync(string phoneNumber, string otpCode);
    Task<bool> SendOtpEmailAsync(string email, string otpCode);
}
