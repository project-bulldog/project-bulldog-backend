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
    private readonly IJwtService _jwtService;
    private readonly ITokenService _tokenService;
    private readonly BulldogDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(IJwtService jwtService, ITokenService tokenService, BulldogDbContext context, ILogger<AuthService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _jwtService = jwtService;
        _tokenService = tokenService;
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AuthResponse> LoginAsync(User user, HttpResponse httpResponse)
    {
        var accessToken = _jwtService.GenerateToken(user);
        var (encrypted, hashed, _) = _tokenService.GenerateRefreshToken();
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
            SameSite = SameSiteMode.Lax,
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

    public async Task LogoutAllSessionsAsync(Guid userId, HttpResponse response)
    {
        await _tokenService.RevokeAllUserTokensAsync(userId, "Manual logout");

        response.Cookies.Append("refreshToken", "", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(-1)
        });

        _logger.LogInformation("User {UserId} logged out of all sessions", userId);
    }

}
