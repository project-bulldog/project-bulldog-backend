using backend.Data;
using backend.Dtos.Auth;
using backend.Dtos.Users;
using backend.Models;
using backend.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly BulldogDbContext _context;
    private readonly JwtService _jwt;

    public AuthController(BulldogDbContext context, JwtService jwt)
    {
        _context = context;
        _jwt = jwt;
    }


    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(CreateUserDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Email already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            DisplayName = dto.DisplayName
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _jwt.GenerateToken(user);

        var response = new AuthResponse(token, new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName
        });

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
            return Unauthorized();

        var token = _jwt.GenerateToken(user);
        return Ok(new { token });
    }
}
