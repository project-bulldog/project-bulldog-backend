using backend.Dtos.Auth;

namespace backend.Services.Auth.Interfaces;

public interface ITokenService
{
    (string EncryptedToken, string HashedToken, string RawToken) GenerateRefreshToken();
    string DecryptToken(string encryptedToken);
    Task<SessionMetadataDto?> RevokeTokenAsync(string encryptedRefreshToken, Guid userId);
    Task RevokeAllUserTokensAsync(Guid userId, string reason);
    Task<(string NewAccessToken, string NewEncryptedRefreshToken)> ValidateAndRotateRefreshTokenAsync(string encryptedToken, HttpResponse response, string? ipAddress = null, string? userAgent = null);
}

