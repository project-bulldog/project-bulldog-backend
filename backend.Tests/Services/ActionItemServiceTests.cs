using backend.Data;
using backend.Dtos.ActionItems;
using backend.Models;
using backend.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace backend.Tests.Services
{
    public class ActionItemServiceTests : IDisposable
    {
        private readonly BulldogDbContext _context;
        private readonly Mock<ILogger<ActionItemService>> _loggerMock;
        private readonly ActionItemService _service;

        public ActionItemServiceTests()
        {
            var options = new DbContextOptionsBuilder<BulldogDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BulldogDbContext(options);
            _loggerMock = new Mock<ILogger<ActionItemService>>();
            _service = new ActionItemService(_context, _loggerMock.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task GetActionItemsAsync_ShouldReturnAllActionItems()
        {
            // Arrange
            var user = await CreateTestUser();
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);

            // Act
            var result = await _service.GetActionItemsAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal(actionItem.Text, result.First().Text);
        }

        [Fact]
        public async Task GetActionItemAsync_WithValidId_ShouldReturnActionItem()
        {
            // Arrange
            var user = await CreateTestUser();
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);

            // Act
            var result = await _service.GetActionItemAsync(actionItem.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(actionItem.Text, result.Text);
        }

        [Fact]
        public async Task GetActionItemAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var result = await _service.GetActionItemAsync(Guid.NewGuid());

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateActionItemAsync_ShouldCreateNewActionItem()
        {
            // Arrange
            var user = await CreateTestUser();
            var summary = await CreateTestSummary(user);
            var newItem = CreateTestCreateDto(summaryId: summary.Id);

            // Act
            var result = await _service.CreateActionItemAsync(newItem);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(newItem.Text, result.Text);
            Assert.Equal(newItem.DueAt, result.DueAt);
            Assert.False(result.IsDone);
        }

        [Fact]
        public async Task UpdateActionItemAsync_WithValidId_ShouldUpdateActionItem()
        {
            // Arrange
            var user = await CreateTestUser();
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);
            var updateDto = CreateTestUpdateDto();

            // Act
            var result = await _service.UpdateActionItemAsync(actionItem.Id, updateDto);

            // Assert
            Assert.True(result);
            var updatedItem = await _context.ActionItems.FindAsync(actionItem.Id);
            Assert.NotNull(updatedItem);
            Assert.Equal(updateDto.Text, updatedItem.Text);
            Assert.Equal(updateDto.IsDone, updatedItem.IsDone);
            Assert.Equal(updateDto.DueAt, updatedItem.DueAt);
        }

        [Fact]
        public async Task UpdateActionItemAsync_WithInvalidId_ShouldReturnFalse()
        {
            // Arrange
            var updateDto = CreateTestUpdateDto();

            // Act
            var result = await _service.UpdateActionItemAsync(Guid.NewGuid(), updateDto);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteActionItemAsync_WithValidId_ShouldDeleteActionItem()
        {
            // Arrange
            var user = await CreateTestUser();
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);

            // Act
            var result = await _service.DeleteActionItemAsync(actionItem.Id);

            // Assert
            Assert.True(result);
            Assert.Null(await _context.ActionItems.FindAsync(actionItem.Id));
        }

        [Fact]
        public async Task DeleteActionItemAsync_WithInvalidId_ShouldReturnFalse()
        {
            // Act
            var result = await _service.DeleteActionItemAsync(Guid.NewGuid());

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ToggleDoneAsync_WithValidId_ShouldToggleDoneStatus()
        {
            // Arrange
            var user = await CreateTestUser();
            var summary = await CreateTestSummary(user);
            var actionItem = await CreateTestActionItemAsync(summaryId: summary.Id);
            var initialStatus = actionItem.IsDone;

            // Act
            var result = await _service.ToggleDoneAsync(actionItem.Id);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(initialStatus, result.IsDone);

            var updatedItem = await _context.ActionItems.FindAsync(actionItem.Id);
            Assert.NotNull(updatedItem);
            Assert.Equal(result.IsDone, updatedItem.IsDone);
        }

        [Fact]
        public async Task ToggleDoneAsync_WithInvalidId_ShouldReturnNull()
        {
            // Act
            var result = await _service.ToggleDoneAsync(Guid.NewGuid());

            // Assert
            Assert.Null(result);
        }

        #region Helper Methods
        private async Task<User> CreateTestUser()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                DisplayName = "Test User"
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
                SummaryText = summaryText
            };
            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();
            return summary;
        }

        private async Task<ActionItem> CreateTestActionItemAsync(string text = "Test Action Item", bool isDone = false, Guid? summaryId = null)
        {
            var actionItem = new ActionItem
            {
                Text = text,
                IsDone = isDone,
                DueAt = DateTime.UtcNow.AddDays(1),
                SummaryId = summaryId ?? throw new ArgumentNullException(nameof(summaryId), "SummaryId is required")
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
                SummaryId = summaryId ?? throw new ArgumentNullException(nameof(summaryId), "SummaryId is required")
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
    }
}
