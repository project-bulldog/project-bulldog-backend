using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActionItemsController : ControllerBase
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<ActionItemsController> _logger;

    public ActionItemsController(BulldogDbContext context, ILogger<ActionItemsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/actionitems
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActionItem>>> GetActionItems()
    {
        _logger.LogInformation("Fetching all action items");
        return await _context.ActionItems
            .Include(ai => ai.Summary)
            .ToListAsync();
    }

    // GET: api/actionitems/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ActionItem>> GetActionItem(int id)
    {
        _logger.LogInformation("Fetching action item with id {Id}", id);
        var actionItem = await _context.ActionItems
            .Include(ai => ai.Summary)
            .FirstOrDefaultAsync(ai => ai.Id == id);

        if (actionItem == null)
        {
            _logger.LogWarning("Action item with id {Id} not found", id);
            return NotFound();
        }

        return actionItem;
    }

    // POST: api/actionitems
    [HttpPost]
    public async Task<ActionResult<ActionItem>> CreateActionItem(ActionItem item)
    {
        _logger.LogInformation("Creating a new action item");
        _context.ActionItems.Add(item);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Action item created with id {Id}", item.Id);
        return CreatedAtAction(nameof(GetActionItem), new { id = item.Id }, item);
    }

    // PUT: api/actionitems/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateActionItem(int id, ActionItem item)
    {
        if (id != item.Id)
        {
            _logger.LogWarning("Update failed: id {Id} does not match action item id {ItemId}", id, item.Id);
            return BadRequest();
        }

        _context.Entry(item).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Action item with id {Id} updated successfully", id);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.ActionItems.Any(ai => ai.Id == id))
            {
                _logger.LogWarning("Update failed: action item with id {Id} not found", id);
                return NotFound();
            }
            _logger.LogError("Concurrency error occurred while updating action item with id {Id}", id);
            throw;
        }

        return NoContent();
    }

    // DELETE: api/actionitems/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteActionItem(int id)
    {
        _logger.LogInformation("Deleting action item with id {Id}", id);
        var actionItem = await _context.ActionItems.FindAsync(id);
        if (actionItem == null)
        {
            _logger.LogWarning("Delete failed: action item with id {Id} not found", id);
            return NotFound();
        }

        _context.ActionItems.Remove(actionItem);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Action item with id {Id} deleted successfully", id);
        return NoContent();
    }

    // PATCH: api/actionitems/{id}/toggle
    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleDone(int id)
    {
        _logger.LogInformation("Toggling done status for action item with id {Id}", id);
        var item = await _context.ActionItems.FindAsync(id);
        if (item == null)
        {
            _logger.LogWarning("Toggle failed: action item with id {Id} not found", id);
            return NotFound();
        }

        _logger.LogInformation("Toggling done status for action item with id {Id}. Previous value: {Prev}", id, item.IsDone);
        item.IsDone = !item.IsDone;
        await _context.SaveChangesAsync();
        _logger.LogInformation("New value for IsDone: {New}", item.IsDone);

        _logger.LogInformation("Action item with id {Id} toggled to {IsDone}", id, item.IsDone);
        return Ok(item);
    }
}
