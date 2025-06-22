using backend.Enums;
using backend.Models;

namespace backend.Services.Auth.Interfaces;

public interface ITwoFactorService
{
    Task<string> GenerateAndSendOtpAsync(User user, OtpDeliveryMethod method);
    Task<string> GenerateAndSendOtpAsync(User user);
    Task<bool> VerifyOtpAsync(User user, string code);
}
