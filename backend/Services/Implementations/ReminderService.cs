using backend.Data;
using backend.Dtos.Reminders;
using backend.Mappers;
using backend.Models;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class ReminderService : IReminderService
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<ReminderService> _logger;
    private readonly ICurrentUserProvider _currentUserProvider;

    private Guid CurrentUserId => _currentUserProvider.UserId;

    public ReminderService(
        BulldogDbContext context,
        ILogger<ReminderService> logger,
        ICurrentUserProvider currentUserProvider)
    {
        _context = context;
        _logger = logger;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<IEnumerable<ReminderDto>> GetRemindersAsync()
    {
        var reminders = await _context.Reminders
            .Where(r => r.UserId == CurrentUserId)
            .AsNoTracking()
            .ToListAsync();

        return reminders.Select(ReminderMapper.ToDto);
    }

    public async Task<ReminderDto?> GetReminderAsync(Guid id)
    {
        var reminder = await _context.Reminders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == CurrentUserId);

        return reminder == null ? null : ReminderMapper.ToDto(reminder);
    }

    public async Task<ReminderDto> CreateReminderAsync(CreateReminderDto dto)
    {
        if (dto.ActionItemId.HasValue)
        {
            var actionItem = await _context.ActionItems
                .Include(ai => ai.Summary)
                .FirstOrDefaultAsync(ai => ai.Id == dto.ActionItemId.Value);

            if (actionItem == null || actionItem.Summary?.UserId != CurrentUserId)
                throw new UnauthorizedAccessException("Cannot create reminder for an ActionItem you do not own.");
        }

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = CurrentUserId,
            Message = dto.Message,
            ReminderTime = dto.ReminderTime,
            ActionItemId = dto.ActionItemId,
            IsActive = true,
            IsMissed = false,
            SnoozedUntil = null,
            IsSent = false,
            SentAt = null
        };

        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        return ReminderMapper.ToDto(reminder);
    }

    public async Task<bool> UpdateReminderAsync(Guid id, UpdateReminderDto dto)
    {
        var reminder = await _context.Reminders
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == CurrentUserId);
        if (reminder == null) return false;

        if (dto.ActionItemId.HasValue)
        {
            var actionItem = await _context.ActionItems
                .Include(ai => ai.Summary)
                .FirstOrDefaultAsync(ai => ai.Id == dto.ActionItemId.Value);

            if (actionItem == null || actionItem.Summary?.UserId != CurrentUserId)
                throw new UnauthorizedAccessException("Cannot assign reminder to an ActionItem you do not own.");
        }

        reminder.Message = dto.Message;
        reminder.ReminderTime = dto.ReminderTime;
        reminder.ActionItemId = dto.ActionItemId;
        reminder.IsMissed = false;
        reminder.SnoozedUntil = null;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteReminderAsync(Guid id)
    {
        var reminder = await _context.Reminders
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == CurrentUserId);
        if (reminder == null) return false;

        _context.Reminders.Remove(reminder);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SnoozeReminderAsync(Guid reminderId, int snoozeMinutes)
    {
        var reminder = await _context.Reminders
            .Include(r => r.ActionItem)
            .FirstOrDefaultAsync(r => r.Id == reminderId && r.UserId == CurrentUserId);

        if (reminder == null || reminder.ActionItem?.IsDeleted == true)
        {
            _logger.LogWarning("❌ Snooze failed: reminder {ReminderId} not found or task is deleted", reminderId);
            return false;
        }

        reminder.SnoozedUntil = DateTime.UtcNow.AddMinutes(snoozeMinutes);
        reminder.IsActive = true;
        reminder.IsMissed = false;

        _logger.LogInformation("😴 Snoozed reminder {ReminderId} for user {UserId} until {SnoozedUntil}", reminder.Id, CurrentUserId, reminder.SnoozedUntil);

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ReminderDto>> GetMissedRemindersAsync()
    {
        var reminders = await _context.Reminders
            .Where(r => r.UserId == CurrentUserId && r.IsMissed)
            .AsNoTracking()
            .ToListAsync();

        return reminders.Select(ReminderMapper.ToDto);
    }
}

