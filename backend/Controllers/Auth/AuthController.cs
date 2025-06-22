using backend.Dtos.Auth;
using backend.Dtos.Users;
using backend.Enums;
using backend.Extensions;
using backend.Helpers;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using Backend.Dtos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace backend.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly ITwoFactorService _twoFactorService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserService userService, IAuthService authService, ITokenService tokenService, ILogger<AuthController> logger, ITwoFactorService twoFactorService)
    {
        _userService = userService;
        _authService = authService;
        _tokenService = tokenService;
        _logger = logger;
        _twoFactorService = twoFactorService;
    }

    // POST /api/auth/register
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
    {
        try
        {
            var user = await _userService.RegisterUserAsync(new CreateUserDto
            {
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                Password = dto.Password,
                PhoneNumber = dto.PhoneNumber
            });

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                var code = await _twoFactorService.GenerateAndSendOtpAsync(user); // reuse this for phone verification
                var sanitizedPhoneNumber = LogSanitizer.SanitizeForLog(dto.PhoneNumber);
                _logger.LogInformation("Sent phone verification code to {Phone}", sanitizedPhoneNumber);

                return Ok(new
                {
                    auth = (object?)null,
                    phoneVerificationRequired = true,
                    userId = user.Id
                });
            }

            var response = await _authService.LoginAsync(user, Response);
            return Ok(new { auth = response });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed: {Message}", LogSanitizer.SanitizeForLog(ex.Message));
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("verify-phone")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequestDto request)
    {
        var user = await _userService.GetUserEntityAsync(request.UserId);

        if (user == null)
            return Unauthorized("Invalid user.");

        var isValid = await _twoFactorService.VerifyOtpAsync(user, request.Code);
        if (!isValid)
            return Unauthorized("Invalid or expired verification code.");

        _logger.LogInformation("Phone number verified for user {UserId}", user.Id);

        var response = await _authService.LoginAsync(user, Response);
        return Ok(new { auth = response });
    }

    // POST /api/auth/login
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResultDto>> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var user = await _authService.AuthenticateUserAsync(request);
            var result = await _authService.LoginAsync(user, Response);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed: {Message}", LogSanitizer.SanitizeForLog(ex.Message));
            return Unauthorized(ex.Message);
        }
    }

    // POST /api/auth/request-2fa
    [AllowAnonymous]
    [HttpPost("request-2fa")]
    public async Task<IActionResult> RequestTwoFactor([FromBody] RequestTwoFactorDto request)
    {
        try
        {
            var user = await _userService.GetUserEntityAsync(request.UserId);
            if (user == null)
                return Unauthorized("Invalid user.");

            if (!user.TwoFactorEnabled)
                return BadRequest("2FA is not enabled for this user.");

            var code = await _twoFactorService.GenerateAndSendOtpAsync(user, request.Method);

            _logger.LogInformation("2FA code sent via {Method} to user {UserId}", request.Method, user.Id);

            return Ok(new { message = $"Verification code sent via {request.Method}" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("2FA request error: {Message}", LogSanitizer.SanitizeForLog(ex.Message));
            return BadRequest(ex.Message);
        }
    }

    // POST /api/auth/verify-2fa
    [AllowAnonymous]
    [HttpPost("verify-2fa")]
    public async Task<ActionResult<LoginResultDto>> VerifyTwoFactor([FromBody] TwoFactorVerifyRequestDto request)
    {
        try
        {
            var result = await _authService.VerifyTwoFactorAsync(request.UserId, request.Code, Response);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("2FA verification failed: {Message}", LogSanitizer.SanitizeForLog(ex.Message));
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("2FA verification error: {Message}", LogSanitizer.SanitizeForLog(ex.Message));
            return BadRequest(ex.Message);
        }
    }

    // POST /api/auth/refresh
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshRequestDto? request = null)
    {
#if DEBUG
        var rawCookieHeader = LogSanitizer.SanitizeForLog(Request.Headers.Cookie.ToString());
        _logger.LogInformation("Incoming Cookie Header: {RawCookie}", rawCookieHeader);
#endif

        // Distinguish between iOS fallback and normal flow
        var tokenFromBody = request?.Token;
        var tokenFromCookie = Request.Cookies["refreshToken"];
        var encryptedToken = tokenFromBody ?? tokenFromCookie;
        var usedBodyFallback = tokenFromBody != null;

#if DEBUG
        var tokenPreview = LogSanitizer.GetSafeTokenPreview(encryptedToken);
        _logger.LogInformation("Parsed refreshToken ends in: {Preview}", tokenPreview);
        if (usedBodyFallback)
        {
            _logger.LogInformation("📱 Refresh request used fallback via body (likely iOS).");
        }
#else
    _logger.LogDebug("refreshToken received: {TokenPresent}",
        LogSanitizer.FormatPresence(!string.IsNullOrWhiteSpace(encryptedToken)));
#endif

        if (string.IsNullOrEmpty(encryptedToken))
        {
            _logger.LogWarning("❌ No refreshToken received — likely blocked by browser or missing.");
            return Unauthorized("Missing refresh token");
        }

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var agent = Request.Headers.UserAgent.ToString();

            var (accessToken, rotatedRefreshToken) = await _tokenService.ValidateAndRotateRefreshTokenAsync(
                encryptedToken, Response, ip, agent);

            _logger.LogInformation("✅ Refresh successful for IP: {IP}, Agent: {Agent}",
                LogSanitizer.SanitizeForLog(ip ?? "unknown"),
                LogSanitizer.SanitizeForLog(agent));

            RefreshResultDto result = usedBodyFallback
                ? RefreshResultDto.ForIos(accessToken, rotatedRefreshToken)
                : RefreshResultDto.ForNormalBrowser(accessToken);

            return Ok(result);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("❌ Token validation failed: {Error}", LogSanitizer.SanitizeForLog(ex.Message));
            return Unauthorized(ex.Message);
        }
    }

    // POST /api/auth/logout
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutUser()
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var encryptedToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrWhiteSpace(encryptedToken))
            return BadRequest(new { message = "Missing refresh token" });

        var sessionInfo = await _authService.LogoutAsync(userId, encryptedToken, Response);

        return Ok(new
        {
            message = "Logged out of current session",
            session = sessionInfo
        });
    }

    // POST /api/auth/logout-all
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAllSessions()
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        await _authService.LogoutAllSessionsAsync(userId, Response);
        return Ok(new { message = "Logged out of all sessions" });
    }

    // GET /api/auth/me
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = User.GetUserId();

        var userDto = await _userService.GetUserAsync(userId);

        if (userDto == null)
            return Unauthorized();

        return Ok(userDto);
    }
}
