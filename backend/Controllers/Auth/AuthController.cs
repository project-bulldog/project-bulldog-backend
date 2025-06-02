using backend.Dtos.Auth;
using backend.Dtos.Users;
using backend.Extensions;
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
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserService userService, IAuthService authService, ITokenService tokenService, ILogger<AuthController> logger)
    {
        _userService = userService;
        _authService = authService;
        _tokenService = tokenService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(CreateUserDto dto)
    {
        try
        {
            var user = await _userService.RegisterUserAsync(dto);
            var response = await _authService.LoginAsync(user, Response); // issues tokens + sets cookie
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
    }


    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _userService.ValidateUserAsync(request);
            var response = await _authService.LoginAsync(user, Response); // issues tokens + sets cookie
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Login failed: {Message}", ex.Message);
            return Unauthorized(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshRequest? request = null)
    {
        var rawCookieHeader = Request.Headers.Cookie.ToString();
        _logger.LogInformation("🍪 Incoming Cookie Header: {RawCookie}", rawCookieHeader);

        // Use body token if present (iOS Safari fallback), else fallback to cookie
        var encryptedToken = request?.Token ?? Request.Cookies["refreshToken"];
        _logger.LogInformation("🔐 Parsed refreshToken: {RefreshToken}", encryptedToken ?? "null");

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

            _logger.LogInformation("✅ Refresh successful for IP: {IP}, Agent: {Agent}", ip, agent);
            return Ok(new
            {
                accessToken,
                refreshToken = rotatedRefreshToken// ✅ include in response for iOS
            });
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("❌ Token validation failed: {Error}", ex.Message);
            return Unauthorized(ex.Message);
        }
    }


    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAllSessions()
    {
        var userId = User.GetUserId();
        await _authService.LogoutAllSessionsAsync(userId, Response);
        return Ok(new { message = "Logged out of all sessions" });
    }
}
