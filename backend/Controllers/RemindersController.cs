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

    public RemindersController(
        IReminderService reminderService,
        ILogger<RemindersController> logger)
    {
        _reminderService = reminderService;
        _logger = logger;
    }

    // GET: api/reminders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReminderDto>>> GetReminders()
    {
        _logger.LogInformation("Fetching all reminders for current user");
        var reminders = await _reminderService.GetRemindersAsync();
        return Ok(reminders);
    }

    // GET: api/reminders/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReminderDto>> GetReminder(Guid id)
    {
        _logger.LogInformation("Fetching reminder with id {Id}", id);
        var reminder = await _reminderService.GetReminderAsync(id);

        if (reminder == null)
        {
            _logger.LogWarning("Reminder with id {Id} not found or not owned by current user", id);
            return NotFound();
        }

        return Ok(reminder);
    }

    // GET: api/reminders/missed
    [HttpGet("missed")]
    public async Task<ActionResult<IEnumerable<ReminderDto>>> GetMissedReminders()
    {
        _logger.LogInformation("Fetching missed reminders for current user");
        var missedReminders = await _reminderService.GetMissedRemindersAsync();
        return Ok(missedReminders);
    }

    // POST: api/reminders
    [HttpPost]
    public async Task<ActionResult<ReminderDto>> CreateReminder(CreateReminderDto dto)
    {
        _logger.LogInformation("Creating a new reminder for current user");

        try
        {
            var created = await _reminderService.CreateReminderAsync(dto);
            _logger.LogInformation("Reminder created with id {Id}", created.Id);
            return CreatedAtAction(nameof(GetReminder), new { id = created.Id }, created);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Reminder creation failed: {Message}", ex.Message);
            return Forbid();
        }
    }

    // POST: api/reminders/{id}/snooze
    [HttpPost("{id:guid}/snooze")]
    public async Task<IActionResult> SnoozeReminder(Guid id, [FromBody] SnoozeReminderDto dto)
    {
        _logger.LogInformation("Snoozing reminder {Id}", id);

        var success = await _reminderService.SnoozeReminderAsync(id, dto.SnoozeMinutes);
        return success ? NoContent() : NotFound();
    }

    // PUT: api/reminders/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateReminder(Guid id, UpdateReminderDto dto)
    {
        try
        {
            var updated = await _reminderService.UpdateReminderAsync(id, dto);

            if (!updated)
            {
                _logger.LogWarning("Update failed: reminder with id {Id} not found or not owned by user", id);
                return NotFound();
            }

            _logger.LogInformation("Reminder with id {Id} updated successfully", id);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Update failed: {Message}", ex.Message);
            return Forbid();
        }
    }

    // DELETE: api/reminders/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteReminder(Guid id)
    {
        _logger.LogInformation("Deleting reminder with id {Id}", id);

        var deleted = await _reminderService.DeleteReminderAsync(id);
        if (!deleted)
        {
            _logger.LogWarning("Delete failed: reminder with id {Id} not found or not owned by user", id);
            return NotFound();
        }

        _logger.LogInformation("Reminder with id {Id} deleted successfully", id);
        return NoContent();
    }
}
