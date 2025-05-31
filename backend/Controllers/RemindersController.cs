using backend.Dtos.Reminders;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/reminders")]
[Authorize]
public class RemindersController : ControllerBase
{
    private readonly IReminderService _reminderService;
    private readonly ILogger<RemindersController> _logger;

    public RemindersController(IReminderService reminderService, ILogger<RemindersController> logger)
    {
        _reminderService = reminderService;
        _logger = logger;
    }

    // GET: api/reminders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReminderDto>>> GetReminders()
    {
        _logger.LogInformation("Fetching all reminders");
        var reminders = await _reminderService.GetRemindersAsync();
        return Ok(reminders);
    }

    // GET: api/reminders/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ReminderDto>> GetReminder(Guid id)
    {
        _logger.LogInformation("Fetching reminder with id {Id}", id);
        var reminder = await _reminderService.GetReminderAsync(id);

        if (reminder == null)
        {
            _logger.LogWarning("Reminder with id {Id} not found", id);
            return NotFound();
        }

        return Ok(reminder);
    }

    // POST: api/reminders
    [HttpPost]
    public async Task<ActionResult<ReminderDto>> CreateReminder(CreateReminderDto dto)
    {
        _logger.LogInformation("Creating a new reminder");
        var userId = Guid.Parse("45335a13-e1f1-4886-b331-9afc0f85cbf9"); // temporarily hardcoded

        var created = await _reminderService.CreateReminderAsync(dto, userId);

        _logger.LogInformation("Reminder created with id {Id}", created.Id);
        return CreatedAtAction(nameof(GetReminder), new { id = created.Id }, created);
    }

    // PUT: api/reminders/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateReminder(Guid id, UpdateReminderDto dto)
    {
        var updated = await _reminderService.UpdateReminderAsync(id, dto);

        if (!updated)
        {
            _logger.LogWarning("Update failed: reminder with id {Id} not found", id);
            return NotFound();
        }

        _logger.LogInformation("Reminder with id {Id} updated successfully", id);
        return NoContent();
    }

    // DELETE: api/reminders/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReminder(Guid id)
    {
        _logger.LogInformation("Deleting reminder with id {Id}", id);
        var deleted = await _reminderService.DeleteReminderAsync(id);

        if (!deleted)
        {
            _logger.LogWarning("Delete failed: reminder with id {Id} not found", id);
            return NotFound();
        }

        _logger.LogInformation("Reminder with id {Id} deleted successfully", id);
        return NoContent();
    }
}
