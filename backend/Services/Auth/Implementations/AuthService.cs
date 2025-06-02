using backend.Data;
using backend.Dtos.Auth;
using backend.Dtos.Users;
using backend.Models;
using backend.Models.Auth;
using backend.Services.Auth.Interfaces;

namespace backend.Services.Auth.Implementations;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly ITokenService _tokenService;
    private readonly ICookieService _cookieService;
    private readonly BulldogDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(IJwtService jwtService, ITokenService tokenService, ICookieService cookieService, BulldogDbContext context, ILogger<AuthService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _jwtService = jwtService;
        _tokenService = tokenService;
        _cookieService = cookieService;
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AuthResponseDto> LoginAsync(User user, HttpResponse httpResponse)
    {
        var accessToken = _jwtService.GenerateToken(user);
        var (encrypted, hashed, _) = _tokenService.GenerateRefreshToken();
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
        var isIOS = userAgent?.ToLower().Contains("iphone") == true || userAgent?.ToLower().Contains("ipad") == true;


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

        _cookieService.SetRefreshToken(httpResponse, refreshToken);
        _logger.LogInformation("Set refreshToken cookie: {Value}", refreshToken.EncryptedToken);


        _logger.LogInformation("User {Id} logged in and tokens issued.", user.Id);

        // Only return token in body if iOS
        var refreshTokenForClient = isIOS ? encrypted : null;

        return new AuthResponseDto(
            accessToken,
            refreshTokenForClient, //Refresh token the frontend needs for iOS fallback
            new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName
            }
        );
    }

    public async Task LogoutAllSessionsAsync(Guid userId, HttpResponse response)
    {
        await _tokenService.RevokeAllUserTokensAsync(userId, "Manual logout");

        _cookieService.ClearRefreshToken(response);

        _logger.LogInformation("User {UserId} logged out of all sessions", userId);
    }

    public async Task<SessionMetadataDto?> LogoutAsync(Guid userId, string encryptedRefreshToken, HttpResponse response)
    {
        var sessionInfo = await _tokenService.RevokeTokenAsync(encryptedRefreshToken, userId);

        _cookieService.ClearRefreshToken(response);

        _logger.LogInformation("User {UserId} logged out of one session from IP {Ip} with UA {UA}", userId, sessionInfo?.Ip, sessionInfo?.UserAgent);

        return sessionInfo;
    }
}
