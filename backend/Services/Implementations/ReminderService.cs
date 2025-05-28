using backend.Data;
using backend.Dtos.Reminders;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class ReminderService : IReminderService
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(BulldogDbContext context, ILogger<ReminderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<ReminderDto>> GetRemindersAsync()
    {
        return await _context.Reminders
            .AsNoTracking()
            .Select(r => new ReminderDto
            {
                Id = r.Id,
                Message = r.Message,
                ReminderTime = r.ReminderTime,
                IsSent = r.IsSent,
                ActionItemId = r.ActionItemId
            })
            .ToListAsync();
    }

    public async Task<ReminderDto?> GetReminderAsync(Guid id)
    {
        var reminder = await _context.Reminders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reminder == null) return null;

        return new ReminderDto
        {
            Id = reminder.Id,
            Message = reminder.Message,
            ReminderTime = reminder.ReminderTime,
            IsSent = reminder.IsSent,
            ActionItemId = reminder.ActionItemId
        };
    }

    public async Task<ReminderDto> CreateReminderAsync(CreateReminderDto dto, Guid userId)
    {
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = dto.Message,
            ReminderTime = dto.ReminderTime,
            ActionItemId = dto.ActionItemId
        };

        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        return new ReminderDto
        {
            Id = reminder.Id,
            Message = reminder.Message,
            ReminderTime = reminder.ReminderTime,
            IsSent = reminder.IsSent,
            ActionItemId = reminder.ActionItemId
        };
    }

    public async Task<bool> UpdateReminderAsync(Guid id, UpdateReminderDto dto)
    {
        var reminder = await _context.Reminders.FindAsync(id);
        if (reminder == null) return false;

        reminder.Message = dto.Message;
        reminder.ReminderTime = dto.ReminderTime;
        reminder.ActionItemId = dto.ActionItemId;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteReminderAsync(Guid id)
    {
        var reminder = await _context.Reminders.FindAsync(id);
        if (reminder == null) return false;

        _context.Reminders.Remove(reminder);
        await _context.SaveChangesAsync();
        return true;
    }
}
