using backend.Data;
using backend.Dtos.Auth;
using backend.Helpers;
using backend.Mappers;
using backend.Models;
using backend.Models.Auth;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Auth.Implementations;

public class AuthService : IAuthService
{
    private readonly IJwtService _jwtService;
    private readonly ITokenService _tokenService;
    private readonly ICookieService _cookieService;
    private readonly IUserService _userService;
    private readonly ITwoFactorService _twoFactorService;
    private readonly BulldogDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(IJwtService jwtService, ITokenService tokenService, ICookieService cookieService, BulldogDbContext context, ILogger<AuthService> logger, IHttpContextAccessor httpContextAccessor, IUserService userService, ITwoFactorService twoFactorService)
    {
        _jwtService = jwtService;
        _tokenService = tokenService;
        _cookieService = cookieService;
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _userService = userService;
        _twoFactorService = twoFactorService;
    }

    public async Task<LoginResultDto> LoginAsync(User user, HttpResponse httpResponse)
    {
        if (user.TwoFactorEnabled)
        {
            await _twoFactorService.GenerateAndSendOtpAsync(user);
            _logger.LogInformation("2FA required: OTP sent to user {Id}", user.Id);

            return LoginResultDto.FromTwoFactor(new TwoFactorPendingDto
            {
                UserId = user.Id
            });
        }

        var auth = await IssueTokensAsync(user, httpResponse);
        return LoginResultDto.FromAuth(auth);
    }

    public async Task<User> AuthenticateUserAsync(LoginRequestDto dto)
    {
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed: user not found with email {Email}", LogSanitizer.SanitizeForLog(dto.Email));
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: incorrect password for user {Id}", user.Id);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (user.PhoneNumberVerified) return user;

        _logger.LogWarning("Login blocked: phone not verified for user {UserId}", user.Id);
        throw new UnauthorizedAccessException("Please verify your phone number before logging in.");

    }


    public async Task<LoginResultDto> VerifyTwoFactorAsync(Guid userId, string code, HttpResponse response)

    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new UnauthorizedAccessException("Invalid user.");

        if (!user.TwoFactorEnabled)
            throw new InvalidOperationException("2FA is not enabled for this user.");

        var isValid = await _twoFactorService.VerifyOtpAsync(user, code);
        if (!isValid)
            throw new UnauthorizedAccessException("Invalid or expired 2FA code.");

        var auth = await IssueTokensAsync(user, response);
        return LoginResultDto.FromAuth(auth);
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

    private async Task<AuthResponseDto> IssueTokensAsync(User user, HttpResponse httpResponse)
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
        _logger.LogInformation("User {Id} logged in and tokens issued.", user.Id);

        var refreshTokenForClient = isIOS ? encrypted : null;

        return new AuthResponseDto(accessToken, refreshTokenForClient, UserMapper.ToDto(user));
    }
}
