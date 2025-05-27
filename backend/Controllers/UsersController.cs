using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(BulldogDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        _logger.LogInformation("Fetching all users");
        return await _context.Users
            .Include(u => u.Summaries)
            .ThenInclude(s => s.ActionItems)
            .ToListAsync();
    }

    // GET: api/users/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(Guid id)
    {
        _logger.LogInformation("Fetching user with id {Id}", id);
        var user = await _context.Users
            .Include(u => u.Summaries)
            .ThenInclude(s => s.ActionItems)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            _logger.LogWarning("User with id {Id} not found", id);
            return NotFound();
        }

        return user;
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(User user)
    {
        user.Id = Guid.NewGuid(); // Assign new GUID if client doesn't send it
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User created with id {Id}", user.Id);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
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

        _context.Entry(user).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("User with id {Id} updated successfully", id);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Users.Any(u => u.Id == id))
            {
                _logger.LogWarning("Update failed: user with id {Id} not found", id);
                return NotFound();
            }
            _logger.LogError("Concurrency error occurred while updating user with id {Id}", id);
            throw;
        }

        return NoContent();
    }

    // DELETE: api/users/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        _logger.LogInformation("Deleting user with id {Id}", id);
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            _logger.LogWarning("Delete failed: user with id {Id} not found", id);
            return NotFound();
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User with id {Id} deleted successfully", id);
        return NoContent();
    }
}
