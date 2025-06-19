using backend.Data;
using backend.Services.Auth.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/two-factor-debug")]
public class TwoFactorDebugController : ControllerBase
{
    private readonly ITwoFactorService _twoFactorService;
    private readonly BulldogDbContext _context;

    public TwoFactorDebugController(ITwoFactorService twoFactorService, BulldogDbContext context)
    {
        _twoFactorService = twoFactorService;
        _context = context;
    }


    // POST: api/twoFactorDebug/send-code/{userId}
    [HttpPost("send-code/{userId}")]
    public async Task<IActionResult> SendCode(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound("User not found.");

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            return BadRequest("User has no phone number.");

        var code = await _twoFactorService.GenerateAndSendOtpAsync(user);
        return Ok(new { message = "OTP sent", debugCode = code });
    }
}
