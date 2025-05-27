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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task GetUsersAsync_ShouldReturnAllUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new() { Id = Guid.NewGuid(), Email = "test1@test.com", DisplayName = "Test User 1" },
            new() { Id = Guid.NewGuid(), Email = "test2@test.com", DisplayName = "Test User 2" }
        };
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

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
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@test.com", DisplayName = "Test User" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUserAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
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
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@test.com",
            DisplayName = "Test User"
        };

        var summary = new Summary
        {
            Id = 2,
            UserId = userId,
            OriginalText = "Test Summary",
            SummaryText = "Test Summary Text"
        };

        var actionItem = new ActionItem
        {
            Id = 3,
            SummaryId = summary.Id,
            Text = "Test Action Item",
            IsDone = false,
            DueAt = DateTime.UtcNow.AddDays(1)
        };

        summary.ActionItems.Add(actionItem);
        user.Summaries.Add(summary);

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUserAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.Id);
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
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "old@test.com", DisplayName = "Old Name" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var updateDto = new UpdateUserDto
        {
            Email = "new@test.com",
            DisplayName = "New Name"
        };

        // Act
        var result = await _service.UpdateUserAsync(userId, updateDto);

        // Assert
        Assert.True(result);
        var updatedUser = await _context.Users.FindAsync(userId);
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
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@test.com", DisplayName = "Test User" };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.DeleteUserAsync(userId);

        // Assert
        Assert.True(result);
        Assert.Null(await _context.Users.FindAsync(userId));
    }

    [Fact]
    public async Task DeleteUserAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _service.DeleteUserAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }
}
