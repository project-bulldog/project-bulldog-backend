using backend.Data;
using backend.Dtos.Auth;
using backend.Dtos.Users;
using backend.Models;
using backend.Models.Auth;
using backend.Services.Auth.Interfaces;
using Microsoft.AspNetCore.Http;

namespace backend.Services.Auth.Implementations;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwt;
    private readonly ITokenService _tokens;
    private readonly BulldogDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(IJwtService jwt, ITokenService tokens, BulldogDbContext context, ILogger<AuthService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _jwt = jwt;
        _tokens = tokens;
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AuthResponse> LoginAsync(User user, HttpResponse httpResponse)
    {
        var accessToken = _jwt.GenerateToken(user);
        var (encrypted, hashed, _) = _tokens.GenerateRefreshToken();
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            EncryptedToken = encrypted,
            HashedToken = hashed,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ip,
            UserAgent = userAgent
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        httpResponse.Cookies.Append("refreshToken", encrypted, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = refreshToken.ExpiresAt
        });

        _logger.LogInformation("User {Id} logged in and tokens issued.", user.Id);

        return new AuthResponse(accessToken, new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName
        });
    }
}
