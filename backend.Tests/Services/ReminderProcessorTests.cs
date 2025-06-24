using backend.Data;
using backend.Models;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;

public class ReminderProcessorTests : IDisposable
{
    private readonly BulldogDbContext _context;
    private readonly Mock<ILogger<ReminderProcessor>> _loggerMock;
    private readonly TelemetryClient _telemetryClient;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly DbContextOptions<BulldogDbContext> _dbContextOptions;

    public ReminderProcessorTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // ensures clean DB per test
            .Options;

        _context = new BulldogDbContext(options);
        _loggerMock = new Mock<ILogger<ReminderProcessor>>();
        _notificationServiceMock = new Mock<INotificationService>();
        _userServiceMock = new Mock<IUserService>();

        var config = new TelemetryConfiguration
        {
            ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
            TelemetryChannel = new InMemoryChannel { DeveloperMode = true }
        };
        _telemetryClient = new TelemetryClient(config);

        _dbContextOptions = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_UpdatesIsSent_ForDueReminders()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reminder1 = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = "Test reminder 1",
            ReminderTime = DateTime.UtcNow.AddMinutes(-1),
            IsSent = false,
            MaxSendAttempts = 3
        };
        var reminder2 = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = "Test reminder 2",
            ReminderTime = DateTime.UtcNow.AddMinutes(10),
            IsSent = false,
            MaxSendAttempts = 3
        };

        _context.Reminders.AddRange(reminder1, reminder2);
        await _context.SaveChangesAsync();

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(x => x.SendReminderAsync(userId, It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var processor = new ReminderProcessor(_context, _loggerMock.Object, notificationMock.Object, _telemetryClient, _userServiceMock.Object);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updatedReminders = await _context.Reminders.ToListAsync();
        var updatedReminder1 = updatedReminders.Single(r => r.Id == reminder1.Id);
        var updatedReminder2 = updatedReminders.Single(r => r.Id == reminder2.Id);
        Assert.True(updatedReminder1.IsSent);
        Assert.False(updatedReminder2.IsSent);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_SetsSentAtAndDoesNotIncrementSendAttempts_OnSuccess()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = "Success test",
            ReminderTime = now.AddMinutes(-1),
            IsSent = false,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(x => x.SendReminderAsync(userId, It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var processor = new ReminderProcessor(_context, _loggerMock.Object, notificationMock.Object, _telemetryClient, _userServiceMock.Object);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updated = await _context.Reminders.FindAsync(reminder.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsSent);
        Assert.NotNull(updated.SentAt);
        Assert.Equal(0, updated.SendAttempts);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_IncrementsSendAttempts_AndDoesNotSetSentAt_OnFailure()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Message = "Failure test",
            ReminderTime = now.AddMinutes(-1),
            IsSent = false,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        var notificationMock = new Mock<INotificationService>();
        notificationMock
            .Setup(x => x.SendReminderAsync(userId, It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Mock failure"));

        var processor = new ReminderProcessor(_context, _loggerMock.Object, notificationMock.Object, _telemetryClient, _userServiceMock.Object);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updated = await _context.Reminders.FindAsync(reminder.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsSent);
        Assert.Null(updated.SentAt);
        Assert.Equal(1, updated.SendAttempts);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_WithDstChange_RecalculatesReminderTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        // Create a user with Mountain Time (which has DST)
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            TimeZoneId = "America/Denver" // Mountain Time
        };

        // Create an action item due at 2:00 PM Mountain Time on a DST transition day
        var dueDate = new DateTime(2024, 3, 10, 20, 0, 0, DateTimeKind.Utc); // 2:00 PM MST on DST transition
        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test task",
            DueAt = dueDate,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = 60 // 1 hour before
        };

        // Create a reminder that was calculated before DST change
        // This would be 1:00 PM MST (before DST), but should be recalculated to 1:00 PM MDT (after DST)
        var oldReminderTime = new DateTime(2024, 3, 10, 19, 0, 0, DateTimeKind.Utc); // 1:00 PM MST
        var reminder = new Reminder
        {
            Id = reminderId,
            UserId = userId,
            ActionItemId = actionItemId,
            ReminderTime = oldReminderTime,
            Message = "Reminder: Test task",
            IsSent = false,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        using var context = new BulldogDbContext(_dbContextOptions);
        context.Users.Add(user);
        context.ActionItems.Add(actionItem);
        context.Reminders.Add(reminder);
        await context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(user);

        var processor = new ReminderProcessor(
            context,
            _loggerMock.Object,
            _notificationServiceMock.Object,
            _telemetryClient,
            _userServiceMock.Object);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updatedReminder = await context.Reminders.FindAsync(reminderId);
        Assert.NotNull(updatedReminder);

        // The reminder time should have been recalculated for DST
        // 2:00 PM MDT (after DST) - 1 hour = 1:00 PM MDT = 19:00 UTC (not 18:00 UTC)
        var expectedReminderTime = new DateTime(2024, 3, 10, 19, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedReminderTime, updatedReminder.ReminderTime);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_WithDstFallback_RecalculatesReminderTime()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        // Create a user with Eastern Time (which has DST)
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            TimeZoneId = "America/New_York" // Eastern Time
        };

        // Create an action item due in the future
        var dueDate = now.AddDays(1); // Due tomorrow
        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test task",
            DueAt = dueDate,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = 60 // 1 hour before
        };

        // Create a reminder with an incorrect time that will trigger recalculation
        // Set it to be due in the past so it gets processed, but the recalculated time will be in the future
        var oldReminderTime = now.AddHours(-1); // 1 hour ago (incorrect time)
        var reminder = new Reminder
        {
            Id = reminderId,
            UserId = userId,
            ActionItemId = actionItemId,
            ReminderTime = oldReminderTime,
            Message = "Reminder: Test task",
            IsSent = false,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        using var context = new BulldogDbContext(_dbContextOptions);
        context.Users.Add(user);
        context.ActionItems.Add(actionItem);
        context.Reminders.Add(reminder);
        await context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(user);

        var processor = new ReminderProcessor(
            context,
            _loggerMock.Object,
            _notificationServiceMock.Object,
            _telemetryClient,
            _userServiceMock.Object);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updatedReminder = await context.Reminders.FindAsync(reminderId);
        Assert.NotNull(updatedReminder);

        // The reminder time should be recalculated to the correct time (1 hour before due date)
        var expectedReminderTime = dueDate.AddMinutes(-60);
        Assert.Equal(expectedReminderTime, updatedReminder.ReminderTime);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_WithNoDstChange_DoesNotRecalculate()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        // Create a user with UTC (no DST)
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            TimeZoneId = "UTC" // No DST
        };

        // Create an action item due in the future
        var dueDate = now.AddDays(1);
        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test task",
            DueAt = dueDate,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = 60 // 1 hour before
        };

        // Create a reminder with correct time
        var correctReminderTime = dueDate.AddMinutes(-60);
        var reminder = new Reminder
        {
            Id = reminderId,
            UserId = userId,
            ActionItemId = actionItemId,
            ReminderTime = correctReminderTime,
            Message = "Reminder: Test task",
            IsSent = false,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        using var context = new BulldogDbContext(_dbContextOptions);
        context.Users.Add(user);
        context.ActionItems.Add(actionItem);
        context.Reminders.Add(reminder);
        await context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(user);

        var processor = new ReminderProcessor(
            context,
            _loggerMock.Object,
            _notificationServiceMock.Object,
            _telemetryClient,
            _userServiceMock.Object);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updatedReminder = await context.Reminders.FindAsync(reminderId);
        Assert.NotNull(updatedReminder);

        // The reminder time should not have changed
        Assert.Equal(correctReminderTime, updatedReminder.ReminderTime);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_WithRecalculatedTimeInPast_MakesImmediatelyDue()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        // Create a user with Mountain Time
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            TimeZoneId = "America/Denver"
        };

        // Create an action item due in the past
        var dueDate = now.AddHours(-2);
        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test task",
            DueAt = dueDate,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = 60 // 1 hour before
        };

        // Create a reminder that is due now (so it gets processed) but has an incorrect time
        // The DST recalculation will result in a time that's also in the past
        var pastReminderTime = now.AddMinutes(-30); // 30 minutes ago (incorrect time)
        var reminder = new Reminder
        {
            Id = reminderId,
            UserId = userId,
            ActionItemId = actionItemId,
            ReminderTime = pastReminderTime,
            Message = "Reminder: Test task",
            IsSent = false,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        using var context = new BulldogDbContext(_dbContextOptions);
        context.Users.Add(user);
        context.ActionItems.Add(actionItem);
        context.Reminders.Add(reminder);
        await context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(user);

        var processor = new ReminderProcessor(
            context,
            _loggerMock.Object,
            _notificationServiceMock.Object,
            _telemetryClient,
            _userServiceMock.Object);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updatedReminder = await context.Reminders.FindAsync(reminderId);
        Assert.NotNull(updatedReminder);

        // The reminder should be made immediately due (exactly 1 second in the future)
        // The ReminderProcessor sets it to now.AddSeconds(1) when recalculated time is in the past
        Assert.True(updatedReminder.ReminderTime > now);
        Assert.True(updatedReminder.ReminderTime <= now.AddSeconds(2));
    }
}
