using backend.Data;
using backend.Services.Auth.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace backend.Controllers;

[ApiController]
[Route("api/two-factor-debug")]
public class TwoFactorDebugController : ControllerBase
{
    private readonly ITwoFactorService _twoFactorService;
    private readonly BulldogDbContext _context;
    private readonly IConfiguration _configuration;

    public TwoFactorDebugController(ITwoFactorService twoFactorService, BulldogDbContext context, IConfiguration configuration)
    {
        _twoFactorService = twoFactorService;
        _context = context;
        _configuration = configuration;
    }

    // GET: api/two-factor-debug/config
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var accountSid = _configuration["Twilio:AccountSid"];
        var authToken = _configuration["Twilio:AuthToken"];
        var fromNumber = _configuration["Twilio:FromNumber"];

        return Ok(new
        {
            accountSidPresent = !string.IsNullOrEmpty(accountSid),
            authTokenPresent = !string.IsNullOrEmpty(authToken),
            fromNumberPresent = !string.IsNullOrEmpty(fromNumber),
            accountSidPreview = !string.IsNullOrEmpty(accountSid) ? accountSid.Substring(0, Math.Min(10, accountSid.Length)) + "..." : "Missing",
            fromNumber = fromNumber ?? "Missing"
        });
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
