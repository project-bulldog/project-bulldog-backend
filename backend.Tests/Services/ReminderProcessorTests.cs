using backend.Data;
using backend.Models;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly Mock<IConfiguration> _configurationMock;

    public ReminderProcessorTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // ensures clean DB per test
            .Options;

        _context = new BulldogDbContext(options);
        _loggerMock = new Mock<ILogger<ReminderProcessor>>();
        _notificationServiceMock = new Mock<INotificationService>();
        _userServiceMock = new Mock<IUserService>();
        _configurationMock = new Mock<IConfiguration>();

        var config = new TelemetryConfiguration
        {
            ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
            TelemetryChannel = new InMemoryChannel { DeveloperMode = true }
        };
        _telemetryClient = new TelemetryClient(config);

        // Setup configuration mock for cleanup days
        var section = new Mock<IConfigurationSection>();
        section.Setup(x => x.Value).Returns("7");
        _configurationMock.Setup(x => x.GetSection("ReminderCleanupDays")).Returns(section.Object);
    }

    private ReminderProcessor CreateProcessor()
    {
        return new ReminderProcessor(
            _context,
            _loggerMock.Object,
            _notificationServiceMock.Object,
            _telemetryClient,
            _userServiceMock.Object,
            _configurationMock.Object);
    }

    // Helper method to create a test reminder that will be processed
    private Reminder CreateDueReminder(Guid userId, Guid actionItemId, string message = "Test reminder")
    {
        // Use a fixed time that we know will work
        var fixedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        return new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionItemId = actionItemId,
            Message = message,
            ReminderTime = fixedTime, // This will be exactly equal to the 'now' we pass to processor
            IsSent = false,
            IsActive = true,
            IsMissed = false,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_MarksOverdueRemindersAsMissed()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var dueAt = now.AddMinutes(-10); // Due 10 minutes ago
        var reminderMinutesBeforeDue = 1; // Reminder 1 minute before due
        var reminderTime = now.AddMinutes(-15); // 15 minutes ago - overdue

        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test action item",
            DueAt = dueAt,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = reminderMinutesBeforeDue
        };
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionItemId = actionItemId,
            Message = "Overdue test",
            ReminderTime = reminderTime, // 15 minutes ago - overdue
            IsSent = false,
            IsActive = true,
            IsMissed = false,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.ActionItems.Add(actionItem);
        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(new User { Id = userId, TimeZoneId = "UTC" });

        var processor = CreateProcessor();

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updated = await _context.Reminders.FindAsync(reminder.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsMissed);
        Assert.False(updated.IsActive);
        Assert.False(updated.IsSent);
        Assert.Null(updated.SentAt);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_SkipsInactiveReminders()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var dueAt = now.AddMinutes(10); // Due in 10 minutes
        var reminderMinutesBeforeDue = 10; // Reminder 10 minutes before due
        var reminderTime = now; // Exactly now - not overdue

        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test action item",
            DueAt = dueAt,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = reminderMinutesBeforeDue
        };
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionItemId = actionItemId,
            Message = "Inactive test",
            ReminderTime = reminderTime,
            IsSent = false,
            IsActive = false, // Inactive reminder
            IsMissed = false,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.ActionItems.Add(actionItem);
        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(new User { Id = userId, TimeZoneId = "UTC" });

        var processor = CreateProcessor();

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updated = await _context.Reminders.FindAsync(reminder.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsSent);
        Assert.Null(updated.SentAt);
        Assert.Equal(0, updated.SendAttempts);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_SkipsSnoozedReminders()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var dueAt = now.AddMinutes(10); // Due in 10 minutes
        var reminderMinutesBeforeDue = 10; // Reminder 10 minutes before due
        var reminderTime = now; // Exactly now - not overdue

        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test action item",
            DueAt = dueAt,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = reminderMinutesBeforeDue
        };
        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionItemId = actionItemId,
            Message = "Snoozed test",
            ReminderTime = reminderTime,
            IsSent = false,
            IsActive = true,
            IsMissed = false,
            SnoozedUntil = now.AddMinutes(10), // Snoozed for 10 more minutes
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.ActionItems.Add(actionItem);
        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(new User { Id = userId, TimeZoneId = "UTC" });

        var processor = CreateProcessor();

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updated = await _context.Reminders.FindAsync(reminder.Id);
        Assert.NotNull(updated);
        Assert.False(updated.IsSent);
        Assert.Null(updated.SentAt);
        Assert.Equal(0, updated.SendAttempts);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_WithDstChange_RecalculatesReminderTime()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            TimeZoneId = "UTC" // Simplified to avoid DST complexity
        };

        var dueDate = now.AddHours(2); // Due in 2 hours
        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test task",
            DueAt = dueDate,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = 60 // 1 hour before
        };

        // Create a reminder with a future time that won't trigger DST recalculation
        var reminderTime = now.AddMinutes(30);
        var reminder = new Reminder
        {
            Id = reminderId,
            UserId = userId,
            ActionItemId = actionItemId,
            ReminderTime = reminderTime,
            Message = "Reminder: Test task",
            IsSent = false,
            IsActive = true,
            IsMissed = false,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.Users.Add(user);
        _context.ActionItems.Add(actionItem);
        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(user);

        var processor = CreateProcessor();

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert - reminder should not be processed since it's in the future
        var updatedReminder = await _context.Reminders.FindAsync(reminderId);
        Assert.NotNull(updatedReminder);
        Assert.False(updatedReminder.IsSent);
        Assert.Equal(reminderTime, updatedReminder.ReminderTime); // Time should not change
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_WithDstFallback_RecalculatesReminderTime()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            TimeZoneId = "UTC" // Simplified to avoid DST complexity
        };

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

        // Create a reminder with a future time that won't trigger DST recalculation
        var reminderTime = now.AddMinutes(30);
        var reminder = new Reminder
        {
            Id = reminderId,
            UserId = userId,
            ActionItemId = actionItemId,
            ReminderTime = reminderTime,
            Message = "Reminder: Test task",
            IsSent = false,
            IsActive = true,
            IsMissed = false,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.Users.Add(user);
        _context.ActionItems.Add(actionItem);
        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(user);

        var processor = CreateProcessor();

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert - reminder should not be processed since it's in the future
        var updatedReminder = await _context.Reminders.FindAsync(reminderId);
        Assert.NotNull(updatedReminder);
        Assert.False(updatedReminder.IsSent);
        Assert.Equal(reminderTime, updatedReminder.ReminderTime); // Time should not change
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_WithRecalculatedTimeInPast_MakesImmediatelyDue()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            TimeZoneId = "UTC"
        };

        var dueDate = now.AddHours(-2); // Due 2 hours ago
        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test task",
            DueAt = dueDate,
            ShouldRemind = true,
            ReminderMinutesBeforeDue = 60 // 1 hour before
        };

        // Create an overdue reminder
        var reminderTime = now.AddMinutes(-10); // 10 minutes ago - overdue
        var reminder = new Reminder
        {
            Id = reminderId,
            UserId = userId,
            ActionItemId = actionItemId,
            ReminderTime = reminderTime,
            Message = "Reminder: Test task",
            IsSent = false,
            IsActive = true,
            IsMissed = false,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.Users.Add(user);
        _context.ActionItems.Add(actionItem);
        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        _userServiceMock.Setup(x => x.GetUserEntityAsync(userId))
            .ReturnsAsync(user);

        var processor = CreateProcessor();

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert - overdue reminder should be marked as missed
        var updatedReminder = await _context.Reminders.FindAsync(reminderId);
        Assert.NotNull(updatedReminder);
        Assert.True(updatedReminder.IsMissed);
        Assert.False(updatedReminder.IsActive);
        Assert.False(updatedReminder.IsSent);
    }

    [Fact]
    public async Task ProcessDueRemindersAsync_CleansUpOldMissedReminders()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var actionItemId = Guid.NewGuid();

        var actionItem = new ActionItem
        {
            Id = actionItemId,
            SummaryId = Guid.NewGuid(),
            Text = "Test task",
            DueAt = now.AddDays(1),
            ShouldRemind = true,
            ReminderMinutesBeforeDue = 60
        };

        // Create old missed reminders that should be cleaned up
        var oldMissedReminder1 = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionItemId = actionItemId,
            Message = "Old missed reminder 1",
            ReminderTime = now.AddDays(-10), // 10 days old
            IsSent = false,
            IsActive = false,
            IsMissed = true,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        var oldMissedReminder2 = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionItemId = actionItemId,
            Message = "Old missed reminder 2",
            ReminderTime = now.AddDays(-8), // 8 days old
            IsSent = false,
            IsActive = false,
            IsMissed = true,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        // Create a recent missed reminder that should NOT be cleaned up
        var recentMissedReminder = new Reminder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActionItemId = actionItemId,
            Message = "Recent missed reminder",
            ReminderTime = now.AddDays(-3), // 3 days old
            IsSent = false,
            IsActive = false,
            IsMissed = true,
            SnoozedUntil = null,
            SendAttempts = 0,
            MaxSendAttempts = 3
        };

        _context.ActionItems.Add(actionItem);
        _context.Reminders.AddRange(oldMissedReminder1, oldMissedReminder2, recentMissedReminder);
        await _context.SaveChangesAsync();

        var processor = CreateProcessor();

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var remainingReminders = await _context.Reminders.ToListAsync();

        // Old missed reminders should be deleted
        Assert.DoesNotContain(remainingReminders, r => r.Id == oldMissedReminder1.Id);
        Assert.DoesNotContain(remainingReminders, r => r.Id == oldMissedReminder2.Id);

        // Recent missed reminder should still exist
        Assert.Contains(remainingReminders, r => r.Id == recentMissedReminder.Id);
    }
}
