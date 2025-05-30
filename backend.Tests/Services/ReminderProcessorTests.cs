using backend.Data;
using backend.Models;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;

public class ReminderProcessorTests : IDisposable
{
    private readonly BulldogDbContext _context;
    private readonly Mock<ILogger<ReminderProcessor>> _loggerMock;
    private readonly TelemetryClient _telemetryClient;

    public ReminderProcessorTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // ensures clean DB per test
            .Options;

        _context = new BulldogDbContext(options);
        _loggerMock = new Mock<ILogger<ReminderProcessor>>();

        var config = new TelemetryConfiguration
        {
            InstrumentationKey = "00000000-0000-0000-0000-000000000000",
            TelemetryChannel = new InMemoryChannel { DeveloperMode = true }
        };
        _telemetryClient = new TelemetryClient(config);
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

        var processor = new ReminderProcessor(_context, _loggerMock.Object, notificationMock.Object, _telemetryClient);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updatedReminders = await _context.Reminders.ToListAsync();
        Assert.True(updatedReminders.Single(r => r.Id == reminder1.Id).IsSent);
        Assert.False(updatedReminders.Single(r => r.Id == reminder2.Id).IsSent);
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

        var processor = new ReminderProcessor(_context, _loggerMock.Object, notificationMock.Object, _telemetryClient);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updated = await _context.Reminders.FindAsync(reminder.Id);
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

        var processor = new ReminderProcessor(_context, _loggerMock.Object, notificationMock.Object, _telemetryClient);

        // Act
        await processor.ProcessDueRemindersAsync();

        // Assert
        var updated = await _context.Reminders.FindAsync(reminder.Id);
        Assert.False(updated.IsSent);
        Assert.Null(updated.SentAt);
        Assert.Equal(1, updated.SendAttempts);
    }
}
