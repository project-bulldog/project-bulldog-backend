using backend.Dtos.Users;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
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
    public async Task<ActionResult<UserDto>> CreateUser(User user)
    {
        var createdUser = await _userService.CreateUserAsync(user);
        _logger.LogInformation("User created with id {Id}", createdUser.Id);
        return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, createdUser);
    }

    // PUT: api/users/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, User user)
    {
        if (id != user.Id)
        {
            _logger.LogWarning("Update failed: id {Id} does not match user id {UserId}", id, user.Id);
            return BadRequest();
        }

        var updateResult = await _userService.UpdateUserAsync(id, user);

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
}
