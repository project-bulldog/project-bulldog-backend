using backend.Dtos.ActionItems;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace backend.Controllers;

[ApiController]
[Route("api/actionitems")]
[Authorize]
public class ActionItemsController : ControllerBase
{
    private readonly IActionItemService _actionItemService;
    private readonly ILogger<ActionItemsController> _logger;

    public ActionItemsController(IActionItemService actionItemService, ILogger<ActionItemsController> logger)
    {
        _actionItemService = actionItemService;
        _logger = logger;
    }

    // GET: api/actionitems
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActionItemDto>>> GetActionItems()
    {
        _logger.LogInformation("Fetching all action items");
        var actionItems = await _actionItemService.GetActionItemsAsync();
        return Ok(actionItems);
    }

    // GET: api/actionitems/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ActionItemDto>> GetActionItem(Guid id)
    {
        _logger.LogInformation("Fetching action item with id {Id}", id);
        var actionItem = await _actionItemService.GetActionItemAsync(id);

        if (actionItem == null)
        {
            _logger.LogWarning("Action item with id {Id} not found", id);
            return NotFound();
        }

        return Ok(actionItem);
    }

    // POST: api/actionitems
    [HttpPost]
    public async Task<ActionResult<ActionItemDto>> CreateActionItem(CreateActionItemDto itemDto)
    {
        _logger.LogInformation("Creating a new action item");
        var createdItem = await _actionItemService.CreateActionItemAsync(itemDto);
        return CreatedAtAction(nameof(GetActionItem), new { id = createdItem.Id }, createdItem);
    }

    // PUT: api/actionitems/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateActionItem(Guid id, UpdateActionItemDto itemDto)
    {
        var updateResult = await _actionItemService.UpdateActionItemAsync(id, itemDto);

        if (!updateResult)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE: api/actionitems/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteActionItem(Guid id)
    {
        _logger.LogInformation("Deleting action item with id {Id}", id);
        var deleteResult = await _actionItemService.DeleteActionItemAsync(id);

        if (!deleteResult)
        {
            _logger.LogWarning("Delete failed: action item with id {Id} not found", id);
            return NotFound();
        }

        return NoContent();
    }

    // PATCH: api/actionitems/{id}/toggle
    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleDone(Guid id)
    {
        _logger.LogInformation("Toggling done status for action item with id {Id}", id);
        var toggledItem = await _actionItemService.ToggleDoneAsync(id);

        if (toggledItem == null)
        {
            _logger.LogWarning("Toggle failed: action item with id {Id} not found", id);
            return NotFound();
        }

        return Ok(toggledItem);
    }
}
