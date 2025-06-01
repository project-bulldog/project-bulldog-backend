using backend.Dtos.Auth;
using backend.Dtos.Users;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
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
    public async Task<IActionResult> RefreshToken()
    {
        var encryptedToken = Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(encryptedToken))
            return Unauthorized("Missing refresh token");

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var agent = Request.Headers.UserAgent.ToString();

            var (accessToken, _) = await _tokenService.ValidateAndRotateRefreshTokenAsync(
                encryptedToken, Response, ip, agent);

            return Ok(new { accessToken });
        }
        catch (SecurityTokenException ex)
        {
            return Unauthorized(ex.Message);
        }
    }
}
