using backend.Data;
using backend.Dtos.Users;
using backend.Models;
using backend.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;

public class UserServiceTests : IDisposable
{
    private readonly Mock<ILogger<UserService>> _loggerMock;
    private readonly BulldogDbContext _context;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _loggerMock = new Mock<ILogger<UserService>>();
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BulldogDbContext(options);
        _service = new UserService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task GetUsersAsync_ShouldReturnAllUsers()
    {
        // Arrange
        var user1 = await CreateTestUser("test1@test.com", "Test User 1");
        var user2 = await CreateTestUser("test2@test.com", "Test User 2");

        // Act
        var result = await _service.GetUsersAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, u => u.Email == "test1@test.com");
        Assert.Contains(result, u => u.Email == "test2@test.com");
    }

    [Fact]
    public async Task GetUserAsync_WithValidId_ShouldReturnUser()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var result = await _service.GetUserAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("test@test.com", result.Email);
    }

    [Fact]
    public async Task GetUserAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _service.GetUserAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserAsync_WithSummariesAndActionItems_ShouldReturnCompleteStructure()
    {
        // Arrange
        var user = await CreateTestUser();
        var summary = await CreateTestSummary(user);
        var actionItem = await CreateTestActionItem(summary);

        // Act
        var result = await _service.GetUserAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("test@test.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);

        // Verify Summary structure
        Assert.Single(result.Summaries);
        var resultSummary = result.Summaries.First();
        Assert.Equal(summary.Id, resultSummary.Id);

        // Verify ActionItem structure
        Assert.Single(resultSummary.ActionItems);
        var resultActionItem = resultSummary.ActionItems.First();
        Assert.Equal(actionItem.Id, resultActionItem.Id);
        Assert.Equal("Test Action Item", resultActionItem.Text);
        Assert.False(resultActionItem.IsDone);
        Assert.Equal(actionItem.DueAt, resultActionItem.DueAt);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldCreateNewUser()
    {
        // Arrange
        var dto = new CreateUserDto
        {
            Email = "new@test.com",
            DisplayName = "New User"
        };

        // Act
        var result = await _service.CreateUserAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(dto.Email, result.Email);
        Assert.Equal(dto.DisplayName, result.DisplayName);
        Assert.Empty(result.Summaries);
    }

    [Fact]
    public async Task UpdateUserAsync_WithValidId_ShouldUpdateUser()
    {
        // Arrange
        var user = await CreateTestUser();
        var updateDto = new UpdateUserDto
        {
            Email = "new@test.com",
            DisplayName = "New Name"
        };

        // Act
        var result = await _service.UpdateUserAsync(user.Id, updateDto);

        // Assert
        Assert.True(result);
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(updateDto.Email, updatedUser.Email);
        Assert.Equal(updateDto.DisplayName, updatedUser.DisplayName);
    }

    [Fact]
    public async Task UpdateUserAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Arrange
        var updateDto = new UpdateUserDto
        {
            Email = "new@test.com",
            DisplayName = "New Name"
        };

        // Act
        var result = await _service.UpdateUserAsync(Guid.NewGuid(), updateDto);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteUserAsync_WithValidId_ShouldDeleteUser()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var result = await _service.DeleteUserAsync(user.Id);

        // Assert
        Assert.True(result);
        Assert.Null(await _context.Users.FindAsync(user.Id));
    }

    [Fact]
    public async Task DeleteUserAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _service.DeleteUserAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    #region Helper Methods
    private async Task<User> CreateTestUser(string email = "test@test.com", string displayName = "Test User")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = displayName
        };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Summary> CreateTestSummary(User user, string originalText = "Test Summary", string summaryText = "Test Summary Text")
    {
        var summary = new Summary
        {
            Id = 2,
            UserId = user.Id,
            OriginalText = originalText,
            SummaryText = summaryText,
            User = user
        };
        await _context.Summaries.AddAsync(summary);
        await _context.SaveChangesAsync();
        return summary;
    }

    private async Task<ActionItem> CreateTestActionItem(Summary summary)
    {
        var actionItem = new ActionItem
        {
            SummaryId = summary.Id,
            Text = "Test Action Item",
            IsDone = false,
            DueAt = DateTime.UtcNow.AddDays(1)
        };
        summary.ActionItems.Add(actionItem);
        await _context.SaveChangesAsync();
        return actionItem;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
    #endregion
}
