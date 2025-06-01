namespace backend.Services.Auth.Interfaces;

public interface ITokenService
{
    (string EncryptedToken, string HashedToken, string RawToken) GenerateRefreshToken();
    string DecryptToken(string encryptedToken);
    Task<(string NewAccessToken, string NewEncryptedRefreshToken)> ValidateAndRotateRefreshTokenAsync(string encryptedToken, HttpResponse response, string? ipAddress = null, string? userAgent = null);
    Task RevokeAllUserTokensAsync(Guid userId, string reason);
}

