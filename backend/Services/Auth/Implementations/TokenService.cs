using System.Security.Cryptography;
using System.Text;
using backend.Data;
using backend.Models.Auth;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace backend.Services.Auth.Implementations;

public class TokenService : ITokenService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<TokenService> _logger;
    private readonly BulldogDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INotificationService _notificationService;

    public TokenService(IDataProtectionProvider provider, ILogger<TokenService> logger, BulldogDbContext context, IJwtService jwtService, IHttpContextAccessor httpContextAccessor, INotificationService notificationService)
    {
        _protector = provider.CreateProtector("TokenService.RefreshToken");
        _logger = logger;
        _context = context;
        _jwtService = jwtService;
        _httpContextAccessor = httpContextAccessor;
        _notificationService = notificationService;
    }

    public (string EncryptedToken, string HashedToken, string RawToken) GenerateRefreshToken()
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var encrypted = _protector.Protect(rawToken);
        var hashed = ComputeSha256(rawToken);
        return (encrypted, hashed, rawToken);
    }

    public string DecryptToken(string encryptedToken)
    {
        try
        {
            return _protector.Unprotect(encryptedToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refresh token decryption failed — possible tampering detected.");

            throw new InvalidOperationException("Invalid token or token has been tampered with.", ex);
        }
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, string reason)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = reason;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<(string NewAccessToken, string NewEncryptedRefreshToken)> ValidateAndRotateRefreshTokenAsync(string encryptedToken, HttpResponse response, string? ipAddress = null, string? userAgent = null)
    {
        var rawToken = await DecryptAndValidateTokenAsync(encryptedToken);
        var hashedToken = ComputeSha256(rawToken);
        var existingToken = await ValidateExistingTokenAsync(hashedToken);

        await ValidateTokenStatusAsync(existingToken);

        // Revoke the used token (rotation)
        existingToken.IsRevoked = true;
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedReason = "Token rotated";

        var newRefreshToken = await CreateNewRefreshTokenAsync(existingToken, ipAddress, userAgent);
        SetRefreshTokenCookie(response, newRefreshToken);

        // Create new access token
        var accessToken = _jwtService.GenerateToken(existingToken.User);

        return (accessToken, newRefreshToken.EncryptedToken);
    }

    #region Private Methods
    private static string ComputeSha256(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
    private async Task<string> DecryptAndValidateTokenAsync(string encryptedToken)
    {
        string rawToken;

        try
        {
            rawToken = DecryptToken(encryptedToken);
        }
        catch
        {
            _logger.LogWarning("Decryption failed — token may be tampered.");
            await _notificationService.SendSecurityAlertAsync(
                "Security Alert: Token Decryption Failed",
                $"Failed to decrypt refresh token at {DateTime.UtcNow}. Possible tampering detected."
            );
            throw new SecurityTokenException("Refresh token is invalid or tampered.");
        }
        return rawToken;
    }

    private async Task<RefreshToken> ValidateExistingTokenAsync(string hashedToken)
    {
        var existingToken = await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.HashedToken == hashedToken);

        if (existingToken == null)
        {
            _logger.LogWarning("Refresh token not found in database — possible forgery.");
            await _notificationService.SendSecurityAlertAsync(
                "Security Alert: Unknown Refresh Token Used",
                $"Unknown refresh token attempted at {DateTime.UtcNow}. Could indicate forgery."
            );
            throw new SecurityTokenException("Refresh token is invalid.");
        }

        return existingToken;
    }

    private async Task ValidateTokenStatusAsync(RefreshToken token)
    {
        if (token.IsRevoked)
        {
            _logger.LogWarning("Refresh token reuse detected for user {UserId}", token.UserId);
            await _notificationService.SendSecurityAlertAsync(
                "Security Alert: Refresh Token Reuse Detected",
                $"Refresh token reuse detected for user ID: {token.UserId} at {DateTime.UtcNow}. All sessions revoked."
            );
            await RevokeAllUserTokensAsync(token.UserId, "Refresh token reuse detected");
            throw new SecurityTokenException("Refresh token reuse detected. All sessions revoked.");
        }

        if (token.ExpiresAt < DateTime.UtcNow)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = "Expired";
            await _context.SaveChangesAsync();

            _logger.LogWarning("Refresh token expired for user {UserId}", token.UserId);
            throw new SecurityTokenException("Refresh token has expired.");
        }
    }

    private async Task<RefreshToken> CreateNewRefreshTokenAsync(RefreshToken existingToken, string? ipAddress, string? userAgent)
    {
        var (newEncrypted, newHashed, _) = GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            UserId = existingToken.UserId,
            EncryptedToken = newEncrypted,
            HashedToken = newHashed,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress,
            UserAgent = userAgent
        };

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync();

        return newRefreshToken;
    }

    private static void SetRefreshTokenCookie(HttpResponse response, RefreshToken token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = token.ExpiresAt
        };

        // ✅ First delete the existing cookie (prevents duplicates)
        response.Cookies.Delete("refreshToken", cookieOptions);

        // ✅ Then set the new one
        response.Cookies.Append("refreshToken", token.EncryptedToken, cookieOptions);
    }
    #endregion
}
