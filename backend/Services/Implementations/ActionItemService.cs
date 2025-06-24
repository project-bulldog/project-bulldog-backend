using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.Summaries;
using backend.Helpers;
using backend.Mappers;
using backend.Models;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

namespace backend.Services.Implementations;

public class ActionItemService : IActionItemService
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<ActionItemService> _logger;
    private readonly ISummaryService _summaryService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUserService _userService;

    private Guid CurrentUserId => _currentUserProvider.UserId;

    public ActionItemService(
        BulldogDbContext context,
        ILogger<ActionItemService> logger,
        ISummaryService summaryService,
        ICurrentUserProvider currentUserProvider,
        IUserService userService)
    {
        _context = context;
        _logger = logger;
        _summaryService = summaryService;
        _currentUserProvider = currentUserProvider;
        _userService = userService;
    }

    public async Task<IEnumerable<ActionItemDto>> GetActionItemsAsync()
    {
        _logger.LogInformation("Fetching all action items for user {UserId}", CurrentUserId);

        var items = await _context.ActionItems
            .AsNoTracking()
            .Include(ai => ai.Summary)
            .Where(ai => ai.Summary!.UserId == CurrentUserId)
            .ToListAsync();

        return ActionItemMapper.ToDtoList(items);
    }

    public async Task<ActionItemDto?> GetActionItemAsync(Guid id)
    {
        _logger.LogInformation("Fetching action item {Id} for user {UserId}", id, CurrentUserId);

        var item = await _context.ActionItems
            .AsNoTracking()
            .Include(ai => ai.Summary)
            .FirstOrDefaultAsync(ai => ai.Id == id && ai.Summary!.UserId == CurrentUserId);

        if (item is null)
        {
            _logger.LogWarning("Action item {Id} not found or not owned by user {UserId}", id, CurrentUserId);
            return null;
        }

        return ActionItemMapper.ToDto(item);
    }

    public async Task<ActionItemDto> CreateActionItemAsync(CreateActionItemDto itemDto)
    {
        _logger.LogInformation("Creating new action item for user {UserId}", CurrentUserId);

        var user = await _userService.GetUserEntityAsync(CurrentUserId);
        var utcNow = DateTime.UtcNow;
        var localNow = TimeZoneHelpers.ConvertToLocal(utcNow, user?.TimeZoneId);

        var summaryId = itemDto.SummaryId ?? (
            await _summaryService.CreateSummaryAsync(new CreateSummaryDto
            {
                OriginalText = itemDto.Text,
                SummaryText = "[Manual Summary]"
            })).Id;

        var item = new ActionItem
        {
            Id = Guid.NewGuid(),
            Text = itemDto.Text,
            DueAt = itemDto.DueAt,
            IsDateOnly = itemDto.IsDateOnly,
            SummaryId = summaryId,
            IsDone = false,
            ShouldRemind = itemDto.ShouldRemind,
            ReminderMinutesBeforeDue = itemDto.ReminderMinutesBeforeDue,
            CreatedAtUtc = utcNow,
            CreatedAtLocal = localNow
        };

        _context.ActionItems.Add(item);
        await _context.SaveChangesAsync();

        // 🔔 Generate reminder if needed
        if (item.DueAt.HasValue && item.ShouldRemind)
        {
            var offset = item.ReminderMinutesBeforeDue ?? 60;
            var reminderTime = CalculateReminderTime(item.DueAt.Value, offset, user?.TimeZoneId);

            var reminder = new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = CurrentUserId,
                ActionItemId = item.Id,
                ReminderTime = reminderTime,
                Message = $"Reminder: {item.Text}",
                CreatedAtUtc = utcNow,
                CreatedAtLocal = localNow,
                MaxSendAttempts = 3,
                IsSent = false
            };

            _context.Reminders.Add(reminder);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Created action item {Id}", item.Id);
        return ActionItemMapper.ToDto(item);
    }



    public async Task<bool> UpdateActionItemAsync(Guid id, UpdateActionItemDto itemDto)
    {
        var item = await _context.ActionItems
            .Include(ai => ai.Summary)
            .FirstOrDefaultAsync(ai => ai.Id == id && ai.Summary!.UserId == CurrentUserId);

        if (item is null)
        {
            _logger.LogWarning("Update failed: action item {Id} not found or not owned by user {UserId}", id, CurrentUserId);
            return false;
        }

        item.Text = itemDto.Text;
        item.IsDone = itemDto.IsDone;
        item.DueAt = itemDto.DueAt;
        item.IsDateOnly = itemDto.IsDateOnly;
        item.ShouldRemind = itemDto.ShouldRemind;
        item.ReminderMinutesBeforeDue = itemDto.ReminderMinutesBeforeDue;

        var existingReminder = await _context.Reminders.FirstOrDefaultAsync(r => r.ActionItemId == item.Id);

        if (item.DueAt.HasValue && item.ShouldRemind)
        {
            var offset = item.ReminderMinutesBeforeDue ?? 60;
            var user = await _userService.GetUserEntityAsync(CurrentUserId);
            var reminderTime = CalculateReminderTime(item.DueAt.Value, offset, user?.TimeZoneId);

            if (existingReminder != null)
            {
                existingReminder.ReminderTime = reminderTime;
                existingReminder.Message = $"Reminder: {item.Text}";
                existingReminder.IsSent = false;
                existingReminder.SendAttempts = 0;
            }
            else
            {
                _context.Reminders.Add(new Reminder
                {
                    Id = Guid.NewGuid(),
                    UserId = item.Summary!.UserId,
                    ActionItemId = item.Id,
                    ReminderTime = reminderTime,
                    Message = $"Reminder: {item.Text}",
                    MaxSendAttempts = 3,
                    IsSent = false
                });
            }
        }
        else if (existingReminder != null)
        {
            _context.Reminders.Remove(existingReminder);
        }

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated action item {Id}", id);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogError("Concurrency error while updating action item {Id}", id);
            throw;
        }
    }



    public async Task<bool> DeleteActionItemAsync(Guid id)
    {
        _logger.LogInformation("Deleting action item {Id}", id);

        var item = await _context.ActionItems
            .Include(ai => ai.Summary)
            .FirstOrDefaultAsync(ai => ai.Id == id && ai.Summary!.UserId == CurrentUserId);

        if (item is null)
        {
            _logger.LogWarning("Delete failed: action item {Id} not found or not owned by user {UserId}", id, CurrentUserId);
            return false;
        }

        // 🔔 Remove associated reminder if one exists
        var reminder = await _context.Reminders.FirstOrDefaultAsync(r => r.ActionItemId == id);
        if (reminder is not null)
        {
            _context.Reminders.Remove(reminder);
            _logger.LogInformation("Also deleted reminder {ReminderId} linked to action item {Id}", reminder.Id, id);
        }

        _context.ActionItems.Remove(item);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted action item {Id}", id);
        return true;
    }


    public async Task<ActionItemDto?> ToggleDoneAsync(Guid id)
    {
        _logger.LogInformation("Toggling done status for action item {Id}", id);

        var item = await _context.ActionItems
            .Include(ai => ai.Summary)
            .FirstOrDefaultAsync(ai => ai.Id == id && ai.Summary!.UserId == CurrentUserId);

        if (item is null)
        {
            _logger.LogWarning("Toggle failed: action item {Id} not found or not owned by user {UserId}", id, CurrentUserId);
            return null;
        }

        item.IsDone = !item.IsDone;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Toggled IsDone for action item {Id} to {NewValue}", id, item.IsDone);
        return ActionItemMapper.ToDto(item);
    }

    private DateTime CalculateReminderTime(DateTime dueAtUtc, int offsetMinutes, string? userTimeZoneId)
    {
        try
        {
            var localDue = TimeZoneHelpers.ConvertToLocal(dueAtUtc, userTimeZoneId);
            var localReminder = localDue.AddMinutes(-offsetMinutes);
            var utcReminder = TimeZoneHelpers.ConvertToUtc(localReminder, userTimeZoneId);

            _logger.LogInformation("🧠 ReminderTime calculated using timezone {TimezoneId}: {UtcTime}", userTimeZoneId, utcReminder);
            return utcReminder;
        }
        catch
        {
            var fallback = dueAtUtc.AddMinutes(-offsetMinutes);
            _logger.LogWarning("⚠️ Failed to calculate reminder in timezone {TimezoneId}, falling back to UTC: {UtcTime}", userTimeZoneId, fallback);
            return fallback;
        }
    }

}
