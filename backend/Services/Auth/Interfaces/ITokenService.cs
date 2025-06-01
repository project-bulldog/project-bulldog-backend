namespace backend.Services.Auth.Interfaces;

public interface ITokenService
{
    (string EncryptedToken, string HashedToken, string RawToken) GenerateRefreshToken();
    string ComputeSha256(string token);
    string DecryptToken(string encryptedToken);
}

