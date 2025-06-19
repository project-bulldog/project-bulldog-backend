using backend.Dtos.Auth;
using backend.Models;

namespace backend.Services.Auth.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(User user, HttpResponse httpResponse);
    Task<LoginResultDto> VerifyTwoFactorAsync(Guid userId, string code, HttpResponse response);
    Task LogoutAllSessionsAsync(Guid userId, HttpResponse response);
    Task<User> AuthenticateUserAsync(LoginRequestDto dto);
    Task<SessionMetadataDto?> LogoutAsync(Guid userId, string encryptedRefreshToken, HttpResponse response);
}
