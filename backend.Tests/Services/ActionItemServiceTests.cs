using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.Summaries;
using backend.Models;
using backend.Services.Auth.Interfaces;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services
{
    public class ActionItemServiceTests : IDisposable
    {
        private readonly BulldogDbContext _context;
        private readonly Mock<ILogger<ActionItemService>> _loggerMock;
        private readonly Mock<ISummaryService> _summaryServiceMock;
        private readonly Mock<ICurrentUserProvider> _currentUserProviderMock;
        private readonly Mock<IUserService> _userServiceMock;
        private readonly ActionItemService _service;
        private readonly Guid _testUserId = Guid.NewGuid();
        private readonly Guid _otherUserId = Guid.NewGuid();

        public ActionItemServiceTests()
        {
            var options = new DbContextOptionsBuilder<BulldogDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _context = new BulldogDbContext(options);
            _loggerMock = new Mock<ILogger<ActionItemService>>();
            _summaryServiceMock = new Mock<ISummaryService>();
            _currentUserProviderMock = new Mock<ICurrentUserProvider>();
            _userServiceMock = new Mock<IUserService>();
            _currentUserProviderMock.Setup(x => x.UserId).Returns(_testUserId);
            _service = new ActionItemService(_context, _loggerMock.Object, _summaryServiceMock.Object, _currentUserProviderMock.Object, _userServiceMock.Object);
        }

        [Fact]
        public async Task GetActionItemsAsync_ShouldOnlyReturnCurrentUserItems()
        {
            // Arrange
            var currentUser = await CreateTestUser(_testUserId);
            var otherUser = await CreateTestUser(_otherUserId);
            var currentUserSummary = await CreateTestSummary(currentUser);
            var otherUserSummary = await CreateTestSummary(otherUser);

            // Create items with explicit text to make them distinguishable
            var currentUserItem = await CreateTestActionItemAsync(
                text: "Current User Item",
                summaryId: currentUserSummary.Id
            );
            var otherUserItem = await CreateTestActionItemAsync(
                text: "Other User Item",
                summaryId: otherUserSummary.Id
            );

            // Act
            var result = await _service.GetActionItemsAsync();

            // Assert
            Assert.Single(result);
            var returnedItem = result.First();
            Assert.Equal("Current User Item", returnedItem.Text);
            Assert.Equal(currentUserSummary.Id, returnedItem.SummaryId);
            Assert.DoesNotContain(result, x => x.Text == "Other User Item");
        }

        [Fact]
        public async Task GetActionItemAsync_WithOtherUserItem_ShouldReturnNull()
        {
            // Arrange
            var otherUser = await CreateTestUser(_otherUserId);
            var otherUserSummary = await CreateTestSummary(otherUser);
            var otherUserItem = await CreateTestActionItemAsync(summaryId: otherUserSummary.Id);

            // Act
            var result = await _service.GetActionItemAsync(otherUserItem.Id);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateActionItemAsync_WithNullSummaryId_ShouldCreateNewSummary()
        {
            // Arrange
            var newSummaryId = Guid.NewGuid();
            var newSummaryDto = new SummaryDto { Id = newSummaryId };
            _summaryServiceMock.Setup(x => x.CreateSummaryAsync(It.IsAny<CreateSummaryDto>()))
                .ReturnsAsync(newSummaryDto);

            var createDto = new CreateActionItemDto
            {
                Text = "New Action Item",
                DueAt = DateTime.UtcNow.AddDays(1)
            };

            // Act
            var result = await _service.CreateActionItemAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(createDto.Text, result.Text);
            Assert.Equal(newSummaryId, result.SummaryId);
            _summaryServiceMock.Verify(x => x.CreateSummaryAsync(It.Is<CreateSummaryDto>(dto =>
                dto.OriginalText == createDto.Text &&
                dto.SummaryText == "[Manual Summary]")), Times.Once);
        }

        [Fact]
        public async Task CreateActionItemAsync_WithReminderSettings_ShouldCreateReminder()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            _userServiceMock.Setup(x => x.GetUserEntityAsync(_testUserId))
                .ReturnsAsync(user);

            var createDto = new CreateActionItemDto
            {
                Text = "Reminder Test Item",
                DueAt = DateTime.UtcNow.AddDays(1),
                SummaryId = summary.Id,
                ShouldRemind = true,
                ReminderMinutesBeforeDue = 30
            };

            // Act
            var result = await _service.CreateActionItemAsync(createDto);

            // Assert
            Assert.NotNull(result);
            var reminder = await _context.Reminders.FirstOrDefaultAsync(r => r.ActionItemId == result.Id);
            Assert.NotNull(reminder);
            Assert.Equal(_testUserId, reminder!.UserId);
            Assert.Equal("Reminder: Reminder Test Item", reminder.Message);
            Assert.Equal(3, reminder.MaxSendAttempts);
            Assert.False(reminder.IsSent);
            Assert.True(reminder.IsActive);
        }

        [Fact]
        public async Task CreateActionItemAsync_WithoutReminderSettings_ShouldNotCreateReminder()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            _userServiceMock.Setup(x => x.GetUserEntityAsync(_testUserId))
                .ReturnsAsync(user);

            var createDto = new CreateActionItemDto
            {
                Text = "No Reminder Item",
                DueAt = DateTime.UtcNow.AddDays(1),
                SummaryId = summary.Id,
                ShouldRemind = false
            };

            // Act
            var result = await _service.CreateActionItemAsync(createDto);

            // Assert
            Assert.NotNull(result);
            var reminder = await _context.Reminders.FirstOrDefaultAsync(r => r.ActionItemId == result.Id);
            Assert.Null(reminder);
        }

        [Fact]
        public async Task CreateActionItemAsync_WithDateOnly_ShouldSetCorrectProperties()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            _userServiceMock.Setup(x => x.GetUserEntityAsync(_testUserId))
                .ReturnsAsync(user);

            var createDto = new CreateActionItemDto
            {
                Text = "Date Only Item",
                DueAt = DateTime.UtcNow.AddDays(1),
                SummaryId = summary.Id,
                IsDateOnly = true,
                ShouldRemind = false
            };

            // Act
            var result = await _service.CreateActionItemAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsDateOnly);
            Assert.False(result.IsDone);
        }

        [Fact]
        public async Task UpdateActionItemAsync_WithOtherUserItem_ShouldReturnFalse()
        {
            // Arrange
            var otherUser = await CreateTestUser(_otherUserId);
            var otherUserSummary = await CreateTestSummary(otherUser);
            var otherUserItem = await CreateTestActionItemAsync(summaryId: otherUserSummary.Id);
            var updateDto = CreateTestUpdateDto();

            // Act
            var result = await _service.UpdateActionItemAsync(otherUserItem.Id, updateDto);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UpdateActionItemAsync_WithValidItem_ShouldUpdateSuccessfully()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);
            var updateDto = new UpdateActionItemDto
            {
                Text = "Updated Text",
                IsDone = true,
                DueAt = DateTime.UtcNow.AddDays(2),
                IsDateOnly = true
            };

            // Act
            var result = await _service.UpdateActionItemAsync(actionItem.Id, updateDto);

            // Assert
            Assert.True(result);
            var updatedItem = await _context.ActionItems.FindAsync(actionItem.Id);
            Assert.NotNull(updatedItem);
            Assert.Equal(updateDto.Text, updatedItem!.Text);
            Assert.Equal(updateDto.IsDone, updatedItem.IsDone);
            Assert.Equal(updateDto.DueAt, updatedItem.DueAt);
            Assert.Equal(updateDto.IsDateOnly, updatedItem.IsDateOnly);
        }

        [Fact]
        public async Task UpdateActionItemAsync_WithReminderSettings_ShouldUpsertReminder()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);
            _userServiceMock.Setup(x => x.GetUserEntityAsync(_testUserId))
                .ReturnsAsync(user);

            var updateDto = new UpdateActionItemDto
            {
                Text = "Updated with Reminder",
                IsDone = false,
                DueAt = DateTime.UtcNow.AddDays(1),
                ShouldRemind = true,
                ReminderMinutesBeforeDue = 45
            };

            // Act
            var result = await _service.UpdateActionItemAsync(actionItem.Id, updateDto);

            // Assert
            Assert.True(result);
            var reminder = await _context.Reminders.FirstOrDefaultAsync(r => r.ActionItemId == actionItem.Id);
            Assert.NotNull(reminder);
            Assert.Equal("Reminder: Updated with Reminder", reminder!.Message);
        }

        [Fact]
        public async Task UpdateActionItemAsync_RemoveReminder_ShouldDeleteReminder()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);

            // Create an existing reminder
            var existingReminder = new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                ActionItemId = actionItem.Id,
                Message = "Existing Reminder",
                ReminderTime = DateTime.UtcNow.AddHours(1)
            };
            _context.Reminders.Add(existingReminder);
            await _context.SaveChangesAsync();

            var updateDto = new UpdateActionItemDto
            {
                Text = "Remove Reminder",
                IsDone = false,
                DueAt = DateTime.UtcNow.AddDays(1),
                ShouldRemind = false
            };

            // Act
            var result = await _service.UpdateActionItemAsync(actionItem.Id, updateDto);

            // Assert
            Assert.True(result);
            var reminder = await _context.Reminders.FirstOrDefaultAsync(r => r.ActionItemId == actionItem.Id);
            Assert.Null(reminder);
        }

        [Fact]
        public async Task ToggleDoneAsync_WithOtherUserItem_ShouldReturnNull()
        {
            // Arrange
            var otherUser = await CreateTestUser(_otherUserId);
            var otherUserSummary = await CreateTestSummary(otherUser);
            var otherUserItem = await CreateTestActionItemAsync(summaryId: otherUserSummary.Id);

            // Act
            var result = await _service.ToggleDoneAsync(otherUserItem.Id);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ToggleDoneAsync_WithValidItem_ShouldToggleSuccessfully()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);
            var initialDoneState = actionItem.IsDone;

            // Act
            var result = await _service.ToggleDoneAsync(actionItem.Id);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(initialDoneState, result.IsDone);
            var updatedItem = await _context.ActionItems.FindAsync(actionItem.Id);
            Assert.NotNull(updatedItem);
            Assert.Equal(result.IsDone, updatedItem!.IsDone);
        }

        [Fact]
        public async Task ToggleDoneAsync_WithReminders_ShouldToggleReminderActivity()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);

            // Create active reminders
            var reminder1 = new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                ActionItemId = actionItem.Id,
                Message = "Test Reminder 1",
                ReminderTime = DateTime.UtcNow.AddHours(1),
                IsActive = true
            };
            var reminder2 = new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                ActionItemId = actionItem.Id,
                Message = "Test Reminder 2",
                ReminderTime = DateTime.UtcNow.AddHours(2),
                IsActive = true
            };
            _context.Reminders.AddRange(reminder1, reminder2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _service.ToggleDoneAsync(actionItem.Id);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsDone);

            var updatedReminders = await _context.Reminders
                .Where(r => r.ActionItemId == actionItem.Id)
                .ToListAsync();

            Assert.All(updatedReminders, r => Assert.False(r.IsActive));
        }

        [Fact]
        public async Task SoftDeleteActionItemAsync_WithValidItem_ShouldSoftDeleteSuccessfully()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);

            // Act
            await _service.SoftDeleteActionItemAsync(actionItem.Id);

            // Assert
            var deletedItem = await _context.ActionItems.FindAsync(actionItem.Id);
            Assert.NotNull(deletedItem);
            Assert.True(deletedItem.IsDeleted);
            Assert.NotNull(deletedItem.DeletedAt);
        }

        [Fact]
        public async Task SoftDeleteActionItemAsync_WithOtherUserItem_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var otherUser = await CreateTestUser(_otherUserId);
            var otherUserSummary = await CreateTestSummary(otherUser);
            var otherUserItem = await CreateTestActionItemAsync(summaryId: otherUserSummary.Id);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.SoftDeleteActionItemAsync(otherUserItem.Id));
        }

        [Fact]
        public async Task SoftDeleteActionItemAsync_WithReminders_ShouldRemoveReminders()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);

            // Create reminders
            var reminder1 = new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                ActionItemId = actionItem.Id,
                Message = "Test Reminder 1",
                ReminderTime = DateTime.UtcNow.AddHours(1)
            };
            var reminder2 = new Reminder
            {
                Id = Guid.NewGuid(),
                UserId = _testUserId,
                ActionItemId = actionItem.Id,
                Message = "Test Reminder 2",
                ReminderTime = DateTime.UtcNow.AddHours(2)
            };
            _context.Reminders.AddRange(reminder1, reminder2);
            await _context.SaveChangesAsync();

            // Act
            await _service.SoftDeleteActionItemAsync(actionItem.Id);

            // Assert
            var reminders = await _context.Reminders
                .Where(r => r.ActionItemId == actionItem.Id)
                .ToListAsync();
            Assert.Empty(reminders);
        }

        [Fact]
        public async Task RestoreActionItemAsync_WithSoftDeletedItem_ShouldRestoreSuccessfully()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);
            await _service.SoftDeleteActionItemAsync(actionItem.Id);

            // Act
            await _service.RestoreActionItemAsync(actionItem.Id);

            // Assert
            var restoredItem = await _context.ActionItems.FindAsync(actionItem.Id);
            Assert.NotNull(restoredItem);
            Assert.False(restoredItem.IsDeleted);
            Assert.Null(restoredItem.DeletedAt);
        }

        [Fact]
        public async Task RestoreActionItemAsync_WithOtherUserItem_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var otherUser = await CreateTestUser(_otherUserId);
            var otherUserSummary = await CreateTestSummary(otherUser);
            var otherUserItem = await CreateTestActionItemAsync(summaryId: otherUserSummary.Id);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.RestoreActionItemAsync(otherUserItem.Id));
        }

        [Fact]
        public async Task GetActionItemAsync_WithValidItem_ShouldReturnItem()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(
                text: "Test Item",
                isDone: true,
                summaryId: summary.Id
            );

            // Act
            var result = await _service.GetActionItemAsync(actionItem.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(actionItem.Id, result!.Id);
            Assert.Equal(actionItem.Text, result.Text);
            Assert.Equal(actionItem.IsDone, result.IsDone);
            Assert.Equal(actionItem.DueAt, result.DueAt);
            Assert.Equal(actionItem.SummaryId, result.SummaryId);
        }

        [Fact]
        public async Task CreateActionItemAsync_WithExistingSummaryId_ShouldCreateItemWithSummary()
        {
            // Arrange
            var user = await CreateTestUser(_testUserId);
            var summary = await CreateTestSummary(user);
            _userServiceMock.Setup(x => x.GetUserEntityAsync(_testUserId))
                .ReturnsAsync(user);

            var createDto = new CreateActionItemDto
            {
                Text = "New Action Item",
                DueAt = DateTime.UtcNow.AddDays(1),
                SummaryId = summary.Id,
                IsDateOnly = true
            };

            // Act
            var result = await _service.CreateActionItemAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(createDto.Text, result.Text);
            Assert.Equal(summary.Id, result.SummaryId);
            Assert.Equal(createDto.DueAt, result.DueAt);
            Assert.True(result.IsDateOnly);
            Assert.False(result.IsDone);
        }



        #region Helper Methods
        private async Task<User> CreateTestUser(Guid userId)
        {
            var user = new User
            {
                Id = userId,
                Email = $"test{userId}@example.com",
                DisplayName = $"Test User {userId}",
                TimeZoneId = "America/New_York"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        private async Task<Summary> CreateTestSummary(User user, string originalText = "Test Original Text", string summaryText = "Test Summary Text")
        {
            var summary = new Summary
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OriginalText = originalText,
                SummaryText = summaryText,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedAtLocal = DateTime.UtcNow // Test data uses UTC
            };
            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();
            return summary;
        }

        private async Task<ActionItem> CreateTestActionItemAsync(string text = "Test Action Item", bool isDone = false, Guid? summaryId = null)
        {
            var actionItem = new ActionItem
            {
                Id = Guid.NewGuid(),
                Text = text,
                IsDone = isDone,
                DueAt = DateTime.UtcNow.AddDays(1),
                SummaryId = summaryId ?? throw new ArgumentNullException(nameof(summaryId), "SummaryId is required"),
                ShouldRemind = false,
                IsDateOnly = false
            };
            _context.ActionItems.Add(actionItem);
            await _context.SaveChangesAsync();
            return actionItem;
        }

        private CreateActionItemDto CreateTestCreateDto(string text = "New Action Item", Guid? summaryId = null)
        {
            return new CreateActionItemDto
            {
                Text = text,
                DueAt = DateTime.UtcNow.AddDays(2),
                SummaryId = summaryId
            };
        }

        private UpdateActionItemDto CreateTestUpdateDto(string text = "Updated Text", bool isDone = true)
        {
            return new UpdateActionItemDto
            {
                Text = text,
                IsDone = isDone,
                DueAt = DateTime.UtcNow.AddDays(3)
            };
        }
        #endregion

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
