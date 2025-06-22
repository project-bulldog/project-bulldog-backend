using backend.Dtos.Auth;
using backend.Dtos.Users;
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
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IAuthService authService,
        ITokenService tokenService,
        ILogger<AuthController> logger,
        ITwoFactorService twoFactorService,
        IEmailVerificationService emailVerificationService)
    {
        _userService = userService;
        _authService = authService;
        _tokenService = tokenService;
        _logger = logger;
        _twoFactorService = twoFactorService;
        _emailVerificationService = emailVerificationService;
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

            try
            {
                // Send email verification link
                await _emailVerificationService.GenerateAndSendVerificationEmailAsync(user);
                _logger.LogInformation("Verification email sent for user ID {UserId}", user.Id);

                return Ok(new
                {
                    message = "Registration successful! Please check your email to verify your account.",
                    emailVerificationRequired = true,
                    emailSent = true,
                    userId = user.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email for user {UserId} after registration.", user.Id);
                return Ok(new
                {
                    message = "Your account was created, but we had trouble sending the verification email. Please try to log in, and you will be given an option to resend it.",
                    emailVerificationRequired = true,
                    emailSent = false,
                    userId = user.Id
                });
            }
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

        // Mark phone as verified
        user.PhoneNumberVerified = true;
        await _userService.UpdateUserAsync(user.Id, new UpdateUserDto { PhoneNumberVerified = true });

        _logger.LogInformation("Phone number verified for user {UserId}", user.Id);

        return Ok(new { message = "Phone number verified successfully" });
    }

    // POST /api/auth/verify-email
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request)
    {
        var user = await _userService.GetUserEntityAsync(request.UserId);

        if (user == null)
            return Unauthorized("Invalid user.");

        var isValid = await _twoFactorService.VerifyOtpAsync(user, request.Code);
        if (!isValid)
            return Unauthorized("Invalid or expired verification code.");

        // Mark email as verified
        user.EmailVerified = true;
        await _userService.UpdateUserAsync(user.Id, new UpdateUserDto { EmailVerified = true });

        _logger.LogInformation("Email verified for user {UserId}", user.Id);

        var response = await _authService.LoginAsync(user, Response);
        return Ok(new { auth = response });
    }

    // GET /api/auth/verify-email
    [HttpGet("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmailToken([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("Missing verification token");
        }

        var isValid = await _emailVerificationService.VerifyEmailTokenAsync(token);
        if (!isValid)
        {
            return BadRequest("Invalid or expired verification token");
        }

        return Ok(new { message = "Email verified successfully! You can now sign in to your account." });
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

    // POST /api/auth/resend-verification-email
    [AllowAnonymous]
    [HttpPost("resend-verification-email")]
    public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequestDto dto)
    {
        var user = await _userService.GetUserByEmailAsync(dto.Email);
        if (user == null)
        {
            // Don't reveal if a user exists or not for security reasons.
            return Ok(new { message = "If an account with that email exists, a new verification link has been sent." });
        }

        if (user.EmailVerified)
        {
            return BadRequest("This email address has already been verified.");
        }

        try
        {
            await _emailVerificationService.GenerateAndSendVerificationEmailAsync(user);
            _logger.LogInformation("Resent verification email for user {UserId}", user.Id);
            return Ok(new { message = "A new verification link has been sent to your email address." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend verification email for user {UserId}", user.Id);
            return StatusCode(500, "A problem occurred while trying to send the email. Please try again later.");
        }
    }
}
