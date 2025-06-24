using backend.Dtos.Users;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeZoneConverter;
namespace backend.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ICurrentUserProvider currentUserProvider, ILogger<UsersController> logger)
    {
        _userService = userService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    // GET: api/users/me
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = _currentUserProvider.UserId;
        _logger.LogInformation("Fetching current user with id {Id}", userId);

        var user = await _userService.GetUserAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Current user with id {Id} not found", userId);
            return NotFound();
        }

        return Ok(user);
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        _logger.LogInformation("Fetching all users");
        var users = await _userService.GetUsersAsync();
        return Ok(users);
    }

    // GET: api/users/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        _logger.LogInformation("Fetching user with id {Id}", id);
        var user = await _userService.GetUserAsync(id);

        if (user == null)
        {
            _logger.LogWarning("User with id {Id} not found", id);
            return NotFound();
        }

        return Ok(user);
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto createDto)
    {
        var createdUser = await _userService.CreateUserAsync(createDto);
        _logger.LogInformation("User created with id {Id}", createdUser.Id);
        return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, createdUser);
    }

    // PUT: api/users/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserDto updateDto)
    {
        var updateResult = await _userService.UpdateUserAsync(id, updateDto);

        if (!updateResult)
        {
            _logger.LogWarning("Update failed: user with id {Id} not found", id);
            return NotFound();
        }

        _logger.LogInformation("User with id {Id} updated successfully", id);
        return NoContent();
    }

    // DELETE: api/users/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        _logger.LogInformation("Deleting user with id {Id}", id);
        var deleteResult = await _userService.DeleteUserAsync(id);

        if (!deleteResult)
        {
            _logger.LogWarning("Delete failed: user with id {Id} not found", id);
            return NotFound();
        }

        _logger.LogInformation("User with id {Id} deleted successfully", id);
        return NoContent();
    }

    // GET: api/users/timezones
    [HttpGet("timezones")]
    public ActionResult<IEnumerable<object>> GetTimeZones()
    {
        var timeZones = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz =>
            {
                try
                {
                    return new
                    {
                        Id = TZConvert.WindowsToIana(tz.Id),
                        tz.DisplayName,
                        tz.StandardName,
                        BaseUtcOffset = tz.BaseUtcOffset.TotalHours
                    };
                }
                catch
                {
                    // Fallback to original ID if conversion fails
                    return new
                    {
                        tz.Id,
                        tz.DisplayName,
                        tz.StandardName,
                        BaseUtcOffset = tz.BaseUtcOffset.TotalHours
                    };
                }
            })
            .GroupBy(tz => tz.Id) // Group by ID to handle duplicates
            .Select(group => group.First()) // Take the first occurrence of each ID
            .OrderBy(tz => tz.BaseUtcOffset)
            .ToList();

        return Ok(timeZones);
    }
}
