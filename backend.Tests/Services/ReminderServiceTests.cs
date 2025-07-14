using backend.Data;
using backend.Dtos.Reminders;
using backend.Models;
using backend.Services.Auth.Interfaces;
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
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock;
    private readonly ReminderService _service;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ReminderServiceTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BulldogDbContext(options);
        _logger = Mock.Of<ILogger<ReminderService>>();
        _currentUserProviderMock = new Mock<ICurrentUserProvider>();
        _currentUserProviderMock.Setup(x => x.UserId).Returns(_testUserId);

        _service = new ReminderService(_context, _logger, _currentUserProviderMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetRemindersAsync_ShouldReturnAllRemindersForCurrentUser()
    {
        // Arrange
        var reminders = new List<Reminder>
        {
            new Reminder { Id = Guid.NewGuid(), UserId = _testUserId, Message = "Test 1", ReminderTime = DateTime.UtcNow, IsSent = false },
            new Reminder { Id = Guid.NewGuid(), UserId = _testUserId, Message = "Test 2", ReminderTime = DateTime.UtcNow, IsSent = true },
            new Reminder { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Message = "Other User", ReminderTime = DateTime.UtcNow, IsSent = false }
        };

        await _context.Reminders.AddRangeAsync(reminders);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetRemindersAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, r => Assert.Equal(_testUserId, reminders.First(rem => rem.Id == r.Id).UserId));
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
    public async Task GetReminderAsync_WhenReminderBelongsToOtherUser_ShouldReturnNull()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            Message = "Other User's Reminder",
            ReminderTime = DateTime.UtcNow,
            IsSent = false
        };
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetReminderAsync(reminder.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateReminderAsync_ShouldCreateAndReturnNewReminder()
    {
        // Arrange
        var createDto = new CreateReminderDto
        {
            Message = "New Reminder",
            ReminderTime = DateTime.UtcNow,
            ActionItemId = null
        };

        // Act
        var result = await _service.CreateReminderAsync(createDto);

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
        Assert.Equal(_testUserId, savedReminder.UserId);
    }

    [Fact]
    public async Task CreateReminderAsync_WithValidActionItem_ShouldCreateAndReturnNewReminder()
    {
        // Arrange
        var actionItem = await CreateTestActionItem();

        var createDto = new CreateReminderDto
        {
            Message = "New Reminder with ActionItem",
            ReminderTime = DateTime.UtcNow,
            ActionItemId = actionItem.Id
        };

        // Act
        var result = await _service.CreateReminderAsync(createDto);

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
    public async Task CreateReminderAsync_WithInvalidActionItem_ShouldThrowException()
    {
        // Arrange
        var invalidActionItemId = Guid.NewGuid();

        var createDto = new CreateReminderDto
        {
            Message = "New Reminder with Invalid ActionItem",
            ReminderTime = DateTime.UtcNow,
            ActionItemId = invalidActionItemId
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateReminderAsync(createDto));

        Assert.Equal("Cannot create reminder for an ActionItem you do not own.", exception.Message);
    }

    [Fact]
    public async Task CreateReminderAsync_WithActionItemFromOtherUser_ShouldThrowException()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var actionItem = await CreateTestActionItem(otherUserId);

        var createDto = new CreateReminderDto
        {
            Message = "New Reminder with Other User's ActionItem",
            ReminderTime = DateTime.UtcNow,
            ActionItemId = actionItem.Id
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateReminderAsync(createDto));

        Assert.Equal("Cannot create reminder for an ActionItem you do not own.", exception.Message);
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
            ActionItemId = null
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
    public async Task UpdateReminderAsync_WithValidActionItem_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var reminder = await CreateTestReminder("Original");
        var actionItem = await CreateTestActionItem();

        var updateDto = new UpdateReminderDto
        {
            Message = "Updated with ActionItem",
            ReminderTime = DateTime.UtcNow.AddDays(1),
            ActionItemId = actionItem.Id
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
    public async Task UpdateReminderAsync_WithInvalidActionItem_ShouldThrowException()
    {
        // Arrange
        var reminder = await CreateTestReminder("Original");
        var invalidActionItemId = Guid.NewGuid();

        var updateDto = new UpdateReminderDto
        {
            Message = "Updated with Invalid ActionItem",
            ReminderTime = DateTime.UtcNow.AddDays(1),
            ActionItemId = invalidActionItemId
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.UpdateReminderAsync(reminder.Id, updateDto));

        Assert.Equal("Cannot assign reminder to an ActionItem you do not own.", exception.Message);
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
            ActionItemId = null
        };

        // Act
        var result = await _service.UpdateReminderAsync(reminderId, updateDto);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateReminderAsync_WhenReminderBelongsToOtherUser_ShouldReturnFalse()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            Message = "Other User's Reminder",
            ReminderTime = DateTime.UtcNow,
            IsSent = false
        };
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        var updateDto = new UpdateReminderDto
        {
            Message = "Updated",
            ReminderTime = DateTime.UtcNow.AddDays(1),
            ActionItemId = null
        };

        // Act
        var result = await _service.UpdateReminderAsync(reminder.Id, updateDto);

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

    [Fact]
    public async Task DeleteReminderAsync_WhenReminderBelongsToOtherUser_ShouldReturnFalse()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            Message = "Other User's Reminder",
            ReminderTime = DateTime.UtcNow,
            IsSent = false
        };
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteReminderAsync(reminder.Id);

        // Assert
        Assert.False(result);

        // Verify it was not deleted
        var existingReminder = await _context.Reminders.FindAsync(reminder.Id);
        Assert.NotNull(existingReminder);
    }

    [Fact]
    public async Task SnoozeReminderAsync_WhenReminderExists_ShouldSnoozeAndReturnTrue()
    {
        // Arrange
        var reminder = await CreateTestReminder("To Snooze");

        // Act
        var result = await _service.SnoozeReminderAsync(reminder.Id, 30);

        // Assert
        Assert.True(result);

        // Verify the snooze in the database
        var snoozedReminder = await _context.Reminders.FindAsync(reminder.Id);
        Assert.NotNull(snoozedReminder);
        Assert.NotNull(snoozedReminder.SnoozedUntil);
        Assert.True(snoozedReminder.IsActive);
        Assert.False(snoozedReminder.IsMissed);
    }

    [Fact]
    public async Task SnoozeReminderAsync_WhenReminderDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var reminderId = Guid.NewGuid();

        // Act
        var result = await _service.SnoozeReminderAsync(reminderId, 30);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SnoozeReminderAsync_WhenReminderBelongsToOtherUser_ShouldReturnFalse()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            Message = "Other User's Reminder",
            ReminderTime = DateTime.UtcNow,
            IsSent = false
        };
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.SnoozeReminderAsync(reminder.Id, 30);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetMissedRemindersAsync_ShouldReturnOnlyMissedRemindersForCurrentUser()
    {
        // Arrange
        var reminders = new List<Reminder>
        {
            new Reminder { Id = Guid.NewGuid(), UserId = _testUserId, Message = "Missed 1", ReminderTime = DateTime.UtcNow, IsMissed = true, IsSent = false },
            new Reminder { Id = Guid.NewGuid(), UserId = _testUserId, Message = "Active 1", ReminderTime = DateTime.UtcNow, IsMissed = false, IsSent = false },
            new Reminder { Id = Guid.NewGuid(), UserId = _testUserId, Message = "Missed 2", ReminderTime = DateTime.UtcNow, IsMissed = true, IsSent = false },
            new Reminder { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Message = "Other User Missed", ReminderTime = DateTime.UtcNow, IsMissed = true, IsSent = false }
        };

        await _context.Reminders.AddRangeAsync(reminders);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetMissedRemindersAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, r => Assert.True(reminders.First(rem => rem.Id == r.Id).IsMissed));
        Assert.All(result, r => Assert.Equal(_testUserId, reminders.First(rem => rem.Id == r.Id).UserId));
    }

    #region Helper Methods
    private async Task<Reminder> CreateTestReminder(string message, DateTime? reminderTime = null, bool isSent = false)
    {
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            Message = message,
            ReminderTime = reminderTime ?? DateTime.UtcNow,
            IsSent = isSent,
            IsActive = true,
            IsMissed = false
        };

        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();
        return reminder;
    }

    private async Task<ActionItem> CreateTestActionItem(Guid? userId = null)
    {
        var summary = new Summary
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? _testUserId,
            OriginalText = "Test Original Text",
            SummaryText = "Test Summary Text",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow
        };

        var actionItem = new ActionItem
        {
            Id = Guid.NewGuid(),
            SummaryId = summary.Id,
            Text = "Test Action Item",
            IsDone = false,
            IsDeleted = false,
            ShouldRemind = true
        };

        await _context.Summaries.AddAsync(summary);
        await _context.ActionItems.AddAsync(actionItem);
        await _context.SaveChangesAsync();
        return actionItem;
    }
    #endregion
}
