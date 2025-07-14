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

        // 🔔 Conditionally generate reminder
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
        }

        await _context.SaveChangesAsync();

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

        if (item.DueAt.HasValue && item.ShouldRemind)
        {
            var user = await _userService.GetUserEntityAsync(CurrentUserId);
            await UpsertReminderAsync(item, user);
        }
        else
        {
            await RemoveReminderAsync(item.Id);
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

    public async Task SoftDeleteActionItemAsync(Guid id)
    {
        var ai = await _context.ActionItems
            .Include(x => x.Summary)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (ai == null)
            throw new KeyNotFoundException($"ActionItem {id} not found");

        if (ai.Summary?.UserId != CurrentUserId)
            throw new UnauthorizedAccessException("You are not authorized to delete this action item.");

        ai.IsDeleted = true;
        ai.DeletedAt = DateTime.UtcNow;

        var reminders = await _context.Reminders
            .Where(r => r.ActionItemId == id)
            .ToListAsync();

        _context.Reminders.RemoveRange(reminders);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Soft-deleted ActionItem {ActionItemId} and removed {ReminderCount} related reminders.", id, reminders.Count);
    }


    public async Task RestoreActionItemAsync(Guid id)
    {
        var ai = await _context.ActionItems
            .IgnoreQueryFilters()
            .Include(x => x.Summary)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ai == null)
            throw new KeyNotFoundException($"ActionItem {id} not found");

        if (ai.Summary?.UserId != CurrentUserId)
            throw new UnauthorizedAccessException("You are not authorized to restore this action item.");

        ai.IsDeleted = false;
        ai.DeletedAt = null;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Restored ActionItem {ActionItemId} for user {UserId}", id, CurrentUserId);
    }


    public async Task<ActionItemDto?> ToggleDoneAsync(Guid id)
    {
        _logger.LogInformation("Toggling done status for action item {Id}", id);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        var item = await _context.ActionItems
            .Include(ai => ai.Summary)
            .FirstOrDefaultAsync(ai => ai.Id == id && ai.Summary!.UserId == CurrentUserId);

        if (item is null)
        {
            _logger.LogWarning("Toggle failed: action item {Id} not found or not owned by user {UserId}", id, CurrentUserId);
            return null;
        }

        item.IsDone = !item.IsDone;

        // Toggle reminder activation to match IsDone state
        var reminders = await _context.Reminders
            .Where(r => r.ActionItemId == id)
            .ToListAsync();

        foreach (var reminder in reminders)
        {
            reminder.IsActive = !item.IsDone;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation("Toggled IsDone for action item {Id} to {NewValue} and updated {ReminderCount} reminders.", id, item.IsDone, reminders.Count);

        return ActionItemMapper.ToDto(item);
    }

    #region Private Methods
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

    private async Task UpsertReminderAsync(ActionItem item, User? user)
    {
        var existingReminder = await _context.Reminders.FirstOrDefaultAsync(r => r.ActionItemId == item.Id);
        var offset = item.ReminderMinutesBeforeDue ?? 60;
        var reminderTime = CalculateReminderTime(item.DueAt!.Value, offset, user?.TimeZoneId);

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

    private async Task RemoveReminderAsync(Guid actionItemId)
    {
        var existingReminder = await _context.Reminders
            .Where(r => r.ActionItemId == actionItemId)
            .ToListAsync();

        if (existingReminder.Any())
        {
            _context.Reminders.RemoveRange(existingReminder);
        }
    }

    #endregion
}
