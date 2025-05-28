using backend.Data;
using backend.Dtos.Summaries;
using backend.Models;
using backend.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;
public class SummaryServiceTests : IDisposable
{
    private readonly Mock<ILogger<SummaryService>> _loggerMock;
    private readonly BulldogDbContext _context;
    private readonly SummaryService _service;

    public SummaryServiceTests()
    {
        _loggerMock = new Mock<ILogger<SummaryService>>();
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BulldogDbContext(options);
        _service = new SummaryService(_context, _loggerMock.Object);
    }


    [Fact]
    public async Task GetSummariesAsync_ShouldReturnAllSummaries()
    {
        // Arrange
        var user = await CreateTestUser();
        var summary1 = await CreateTestSummary(user, "Test 1", "Summary 1");
        var summary2 = await CreateTestSummary(user, "Test 2", "Summary 2");

        // Act
        var result = await _service.GetSummariesAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, s => s.OriginalText == "Test 1");
        Assert.Contains(result, s => s.OriginalText == "Test 2");
        Assert.All(result, s => Assert.Equal("Test User", s.UserDisplayName));
    }

    [Fact]
    public async Task GetSummaryAsync_WithValidId_ShouldReturnSummary()
    {
        // Arrange
        var user = await CreateTestUser();
        var summary = await CreateTestSummary(user);

        // Act
        var result = await _service.GetSummaryAsync(summary.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(summary.Id.ToString(), result.Id.ToString());
        Assert.Equal("Test", result.OriginalText);
        Assert.Equal("Summary", result.SummaryText);
        Assert.Equal("Test User", result.UserDisplayName);
    }

    [Fact]
    public async Task GetSummaryAsync_WithInvalidId_ShouldReturnNull()
    {
        // Act
        var result = await _service.GetSummaryAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateSummaryAsync_ShouldCreateNewSummary()
    {
        // Arrange
        var user = await CreateTestUser();
        var dto = new CreateSummaryDto
        {
            OriginalText = "Test",
            SummaryText = "Summary",
            UserId = user.Id
        };

        // Act
        var result = await _service.CreateSummaryAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual<Guid>(Guid.Empty, result.Id);
        Assert.Equal("Test", result.OriginalText);
        Assert.Equal("Summary", result.SummaryText);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("Test User", result.UserDisplayName);
        Assert.Empty(result.ActionItems);
    }

    [Fact]
    public async Task UpdateSummaryAsync_WithValidId_ShouldUpdateSummary()
    {
        // Arrange
        var user = await CreateTestUser();
        var summary = await CreateTestSummary(user);
        var updateDto = new UpdateSummaryDto
        {
            OriginalText = "New Text",
            SummaryText = "New Summary"
        };

        // Act
        var result = await _service.UpdateSummaryAsync(summary.Id, updateDto);

        // Assert
        Assert.True(result);
        var updatedSummary = await _context.Summaries.FindAsync(summary.Id);
        Assert.NotNull(updatedSummary);
        Assert.Equal("New Text", updatedSummary!.OriginalText);
        Assert.Equal("New Summary", updatedSummary.SummaryText);
    }

    [Fact]
    public async Task UpdateSummaryAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Arrange
        var updateDto = new UpdateSummaryDto
        {
            OriginalText = "New Text",
            SummaryText = "New Summary"
        };

        // Act
        var result = await _service.UpdateSummaryAsync(Guid.NewGuid(), updateDto);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteSummaryAsync_WithValidId_ShouldDeleteSummary()
    {
        // Arrange
        var user = await CreateTestUser();
        var summary = await CreateTestSummary(user);

        // Act
        var result = await _service.DeleteSummaryAsync(summary.Id);

        // Assert
        Assert.True(result);
        Assert.Null(await _context.Summaries.FindAsync(summary.Id));
    }

    [Fact]
    public async Task DeleteSummaryAsync_WithInvalidId_ShouldReturnFalse()
    {
        // Act
        var result = await _service.DeleteSummaryAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetSummaryAsync_WithActionItems_ShouldReturnSummaryWithActionItems()
    {
        // Arrange
        var user = await CreateTestUser();
        var summary = await CreateTestSummary(user);
        var actionItem = await CreateTestActionItem(summary);

        // Act
        var result = await _service.GetSummaryAsync(summary.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.ActionItems);

        var resultActionItem = result.ActionItems.First();
        Assert.Equal(actionItem.Id, resultActionItem.Id);
        Assert.Equal("Action Item", resultActionItem.Text);
        Assert.False(resultActionItem.IsDone);
        Assert.NotNull(resultActionItem.DueAt);
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

    private async Task<Summary> CreateTestSummary(User user, string originalText = "Test", string summaryText = "Summary")
    {
        var summary = new Summary
        {
            Id = Guid.NewGuid(),
            OriginalText = originalText,
            SummaryText = summaryText,
            UserId = user.Id,
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
            Id = Guid.NewGuid(),
            SummaryId = summary.Id,
            Text = "Action Item",
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
