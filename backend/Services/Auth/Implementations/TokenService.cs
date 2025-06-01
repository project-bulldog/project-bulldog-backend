using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace backend.Services.Auth.Implementations;

public class TokenService : Interfaces.ITokenService
{
    private readonly IDataProtector _protector;
    public TokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("TokenService.RefreshToken");
    }

    public (string EncryptedToken, string HashedToken, string RawToken) GenerateRefreshToken()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var encrypted = _protector.Protect(rawToken);
        var hashed = ComputeSha256(rawToken);
        return (encrypted, hashed, rawToken);
    }

    public string ComputeSha256(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    public string DecryptToken(string encryptedToken)
    {
        try
        {
            return _protector.Unprotect(encryptedToken);
        }
        catch (Exception ex)
        {
            // Token has been tampered with or is invalid
            throw new InvalidOperationException("Invalid token or token has been tampered with.", ex);
        }
    }
}
