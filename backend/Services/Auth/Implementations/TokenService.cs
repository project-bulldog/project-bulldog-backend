using System.Security.Cryptography;
using System.Text;
using backend.Data;
using backend.Models.Auth;
using backend.Services.Auth.Interfaces;
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

    public TokenService(IDataProtectionProvider provider, ILogger<TokenService> logger, BulldogDbContext context, IJwtService jwtService, IHttpContextAccessor httpContextAccessor)
    {
        _protector = provider.CreateProtector("TokenService.RefreshToken");
        _logger = logger;
        _context = context;
        _jwtService = jwtService;
        _httpContextAccessor = httpContextAccessor;
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
        string rawToken;

        try
        {
            rawToken = DecryptToken(encryptedToken);
        }
        catch
        {
            _logger.LogWarning("Decryption failed — token may be tampered.");
            throw new SecurityTokenException("Refresh token is invalid or tampered.");
        }

        var hashedToken = ComputeSha256(rawToken);

        var existingToken = await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.HashedToken == hashedToken);

        if (existingToken == null)
        {
            _logger.LogWarning("Refresh token not found in database — possible forgery.");
            throw new SecurityTokenException("Refresh token is invalid.");
        }

        if (existingToken.IsRevoked)
        {
            _logger.LogWarning("Refresh token reuse detected for user {UserId}", existingToken.UserId);

            await RevokeAllUserTokensAsync(existingToken.UserId, "Refresh token reuse detected");
            throw new SecurityTokenException("Refresh token reuse detected. All sessions revoked.");
        }

        if (existingToken.ExpiresAt < DateTime.UtcNow)
        {
            existingToken.IsRevoked = true;
            existingToken.RevokedAt = DateTime.UtcNow;
            existingToken.RevokedReason = "Expired";

            await _context.SaveChangesAsync();

            _logger.LogWarning("Refresh token expired for user {UserId}", existingToken.UserId);
            throw new SecurityTokenException("Refresh token has expired.");
        }

        // Revoke the used token (rotation)
        existingToken.IsRevoked = true;
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedReason = "Token rotated";

        // Generate a new refresh token
        var (newEncrypted, newHashed, newRaw) = GenerateRefreshToken();

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

        // Set new cookie
        response.Cookies.Append("refreshToken", newEncrypted, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = newRefreshToken.ExpiresAt
        });

        // Create new access token
        var accessToken = _jwtService.GenerateToken(existingToken.User);

        return (accessToken, newEncrypted);
    }

}
