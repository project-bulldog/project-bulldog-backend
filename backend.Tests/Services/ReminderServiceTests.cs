using backend.Data;
using backend.Dtos.Reminders;
using backend.Models;
using backend.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class ReminderServiceTests : IDisposable
{
    private readonly BulldogDbContext _context;
    private readonly ILogger<ReminderService> _logger;
    private readonly ReminderService _service;

    public ReminderServiceTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BulldogDbContext(options);
        _logger = Mock.Of<ILogger<ReminderService>>();
        _service = new ReminderService(_context, _logger);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetRemindersAsync_ShouldReturnAllReminders()
    {
        // Arrange
        var reminders = new List<Reminder>
        {
            new Reminder { Id = Guid.NewGuid(), Message = "Test 1", ReminderTime = DateTime.UtcNow, IsSent = false },
            new Reminder { Id = Guid.NewGuid(), Message = "Test 2", ReminderTime = DateTime.UtcNow, IsSent = true }
        };

        await _context.Reminders.AddRangeAsync(reminders);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRemindersAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Equal(reminders[0].Id, result.First().Id);
        Assert.Equal(reminders[1].Id, result.Last().Id);
    }

    [Fact]
    public async Task GetReminderAsync_WhenReminderExists_ShouldReturnReminder()
    {
        // Arrange
        var reminder = await CreateTestReminder("Test");

        // Act
        var result = await _service.GetReminderAsync(reminder.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(reminder.Id, result.Id);
        Assert.Equal(reminder.Message, result.Message);
    }

    [Fact]
    public async Task GetReminderAsync_WhenReminderDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var reminderId = Guid.NewGuid();

        // Act
        var result = await _service.GetReminderAsync(reminderId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReminderAsync_ShouldCreateAndReturnNewReminder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createDto = new CreateReminderDto
        {
            Message = "New Reminder",
            ReminderTime = DateTime.UtcNow,
            ActionItemId = Guid.NewGuid()
        };

        // Act
        var result = await _service.CreateReminderAsync(createDto, userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createDto.Message, result.Message);
        Assert.Equal(createDto.ReminderTime, result.ReminderTime);
        Assert.Equal(createDto.ActionItemId, result.ActionItemId);
        Assert.False(result.IsSent);

        // Verify it was actually saved to the database
        var savedReminder = await _context.Reminders.FindAsync(result.Id);
        Assert.NotNull(savedReminder);
        Assert.Equal(createDto.Message, savedReminder.Message);
    }

    [Fact]
    public async Task UpdateReminderAsync_WhenReminderExists_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var reminder = await CreateTestReminder("Original");

        var updateDto = new UpdateReminderDto
        {
            Message = "Updated",
            ReminderTime = DateTime.UtcNow.AddDays(1),
            ActionItemId = Guid.NewGuid()
        };

        // Act
        var result = await _service.UpdateReminderAsync(reminder.Id, updateDto);

        // Assert
        Assert.True(result);

        // Verify the update in the database
        var updatedReminder = await _context.Reminders.FindAsync(reminder.Id);
        Assert.NotNull(updatedReminder);
        Assert.Equal(updateDto.Message, updatedReminder.Message);
        Assert.Equal(updateDto.ReminderTime, updatedReminder.ReminderTime);
        Assert.Equal(updateDto.ActionItemId, updatedReminder.ActionItemId);
    }

    [Fact]
    public async Task UpdateReminderAsync_WhenReminderDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var reminderId = Guid.NewGuid();
        var updateDto = new UpdateReminderDto
        {
            Message = "Updated",
            ReminderTime = DateTime.UtcNow.AddDays(1),
            ActionItemId = Guid.NewGuid()
        };

        // Act
        var result = await _service.UpdateReminderAsync(reminderId, updateDto);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteReminderAsync_WhenReminderExists_ShouldDeleteAndReturnTrue()
    {
        // Arrange
        var reminder = await CreateTestReminder("To Delete");

        // Act
        var result = await _service.DeleteReminderAsync(reminder.Id);

        // Assert
        Assert.True(result);

        // Verify it was actually deleted from the database
        var deletedReminder = await _context.Reminders.FindAsync(reminder.Id);
        Assert.Null(deletedReminder);
    }

    [Fact]
    public async Task DeleteReminderAsync_WhenReminderDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var reminderId = Guid.NewGuid();

        // Act
        var result = await _service.DeleteReminderAsync(reminderId);

        // Assert
        Assert.False(result);
    }

    #region Helper Methods
    private async Task<Reminder> CreateTestReminder(string message, DateTime? reminderTime = null, bool isSent = false)
    {
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            Message = message,
            ReminderTime = reminderTime ?? DateTime.UtcNow,
            IsSent = isSent
        };

        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();
        return reminder;
    }
    #endregion
}
