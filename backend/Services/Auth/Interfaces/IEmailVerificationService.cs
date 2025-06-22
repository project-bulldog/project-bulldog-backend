using backend.Models;

namespace backend.Services.Auth.Interfaces;

public interface IEmailVerificationService
{
    Task<string> GenerateAndSendVerificationEmailAsync(User user);
    Task<bool> VerifyEmailTokenAsync(string token);
}
