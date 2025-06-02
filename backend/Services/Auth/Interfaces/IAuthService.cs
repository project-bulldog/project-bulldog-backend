using backend.Dtos.Auth;
using backend.Models;

namespace backend.Services.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(User user, HttpResponse httpResponse);
    Task LogoutAllSessionsAsync(Guid userId, HttpResponse response);
    Task<SessionMetadataDto?> LogoutAsync(Guid userId, string encryptedRefreshToken, HttpResponse response);
}
