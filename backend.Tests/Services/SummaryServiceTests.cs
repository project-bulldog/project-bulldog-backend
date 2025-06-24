using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.AiSummaries;
using backend.Dtos.Summaries;
using backend.Models;
using backend.Services.Auth.Interfaces;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;
public class SummaryServiceTests : IDisposable
{
    private readonly Mock<ILogger<SummaryService>> _loggerMock;
    private readonly BulldogDbContext _context;
    private readonly Mock<IAiService> _aiServiceMock;
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock;
    private readonly SummaryService _service;
    private readonly Guid _testUserId = Guid.NewGuid();

    public SummaryServiceTests()
    {
        _loggerMock = new Mock<ILogger<SummaryService>>();
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BulldogDbContext(options);
        _aiServiceMock = new Mock<IAiService>();
        _currentUserProviderMock = new Mock<ICurrentUserProvider>();
        _currentUserProviderMock.Setup(x => x.UserId).Returns(_testUserId);
        _service = new SummaryService(_context, _loggerMock.Object, _aiServiceMock.Object, _currentUserProviderMock.Object);
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
            SummaryText = "Summary"
        };

        // Act
        var result = await _service.CreateSummaryAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual<Guid>(Guid.Empty, result.Id);
        Assert.Equal("Test", result.OriginalText);
        Assert.Equal("Summary", result.SummaryText);
        Assert.Equal(_testUserId, result.UserId);
        Assert.Equal("Test User", result.UserDisplayName);
        Assert.Empty(result.ActionItems);
    }

    [Fact]
    public async Task CreateSummaryAsync_WithActionItems_ShouldCreateSummaryWithActionItems()
    {
        // Arrange
        var user = await CreateTestUser();
        var dto = new CreateSummaryDto
        {
            OriginalText = "Test",
            SummaryText = "Summary",
            ActionItems = new List<CreateActionItemDto>
            {
                new CreateActionItemDto { Text = "Action 1", DueAt = DateTime.UtcNow.AddDays(1) },
                new CreateActionItemDto { Text = "Action 2", DueAt = DateTime.UtcNow.AddDays(2) }
            }
        };

        // Act
        var result = await _service.CreateSummaryAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.ActionItems.Count());
        Assert.Contains(result.ActionItems, ai => ai.Text == "Action 1");
        Assert.Contains(result.ActionItems, ai => ai.Text == "Action 2");
        Assert.All(result.ActionItems, ai => Assert.False(ai.IsDone));
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

    [Fact]
    public async Task GenerateChunkedSummaryWithActionItemsAsync_ShouldCreateSummaryWithActionItems()
    {
        // Arrange
        var user = await CreateTestUser();
        var input = "Test input text";
        var expectedSummary = "Generated summary";
        var expectedActionItems = new List<ActionItemDto>
        {
            new ActionItemDto { Text = "Action item 1", IsDone = false },
            new ActionItemDto { Text = "Action item 2", IsDone = false }
        };

        _aiServiceMock.Setup(x => x.SummarizeAndExtractActionItemsChunkedAsync(It.IsAny<AiChunkedSummaryResponseDto>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        // Act
        var result = await _service.GenerateChunkedSummaryWithActionItemsAsync(input);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(input, result.OriginalText);
        Assert.Equal(expectedSummary, result.SummaryText);
        Assert.Equal(_testUserId, result.UserId);
        Assert.Equal(2, result.ActionItems.Count());
        Assert.Contains(result.ActionItems, ai => ai.Text == "Action item 1");
        Assert.Contains(result.ActionItems, ai => ai.Text == "Action item 2");
        Assert.All(result.ActionItems, ai => Assert.False(ai.IsDone));
    }

    [Fact]
    public async Task GenerateChunkedSummaryWithActionItemsAsync_WithModelOverride_ShouldUseSpecifiedModel()
    {
        // Arrange
        var user = await CreateTestUser();
        var input = "Test input text";
        var expectedSummary = "Generated summary";
        var expectedActionItems = new List<ActionItemDto>
        {
            new ActionItemDto { Text = "Action item 1", IsDone = false }
        };
        var modelOverride = "gpt-4-turbo";

        _aiServiceMock.Setup(x => x.SummarizeAndExtractActionItemsChunkedAsync(It.Is<AiChunkedSummaryResponseDto>(dto =>
            dto.Model == modelOverride)))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        // Act
        var result = await _service.GenerateChunkedSummaryWithActionItemsAsync(input, true, modelOverride);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSummary, result.SummaryText);
        Assert.Single(result.ActionItems);
        _aiServiceMock.Verify(x => x.SummarizeAndExtractActionItemsChunkedAsync(It.Is<AiChunkedSummaryResponseDto>(dto =>
            dto.Model == modelOverride)), Times.Once);
    }

    [Fact]
    public async Task GenerateChunkedSummaryWithActionItemsAsync_WithoutMapReduce_ShouldDisableMapReduce()
    {
        // Arrange
        var user = await CreateTestUser();
        var input = "Test input text";
        var expectedSummary = "Generated summary";
        var expectedActionItems = new List<ActionItemDto>
        {
            new ActionItemDto { Text = "Action item 1", IsDone = false }
        };

        _aiServiceMock.Setup(x => x.SummarizeAndExtractActionItemsChunkedAsync(It.Is<AiChunkedSummaryResponseDto>(dto =>
            dto.UseMapReduce == false)))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        // Act
        var result = await _service.GenerateChunkedSummaryWithActionItemsAsync(input, false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSummary, result.SummaryText);
        Assert.Single(result.ActionItems);
        _aiServiceMock.Verify(x => x.SummarizeAndExtractActionItemsChunkedAsync(It.Is<AiChunkedSummaryResponseDto>(dto =>
            dto.UseMapReduce == false)), Times.Once);
    }

    [Fact]
    public async Task GetSummariesAsync_ShouldOnlyReturnCurrentUserSummaries()
    {
        // Arrange
        var otherUserId = Guid.NewGuid();

        // Create users
        var currentUser = new User
        {
            Id = _testUserId,
            Email = "current@test.com",
            DisplayName = "Current User"
        };
        var otherUser = new User
        {
            Id = otherUserId,
            Email = "other@test.com",
            DisplayName = "Other User"
        };

        await _context.Users.AddAsync(currentUser);
        await _context.Users.AddAsync(otherUser);
        await _context.SaveChangesAsync();

        var currentUserSummary = new Summary
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            OriginalText = "Current User Summary",
            SummaryText = "Summary",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow,
            User = currentUser
        };

        var otherUserSummary = new Summary
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            OriginalText = "Other User Summary",
            SummaryText = "Summary",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow,
            User = otherUser
        };

        // Add summaries to database
        await _context.Summaries.AddAsync(currentUserSummary);
        await _context.Summaries.AddAsync(otherUserSummary);
        await _context.SaveChangesAsync();

        // Verify summaries were saved
        var savedSummaries = await _context.Summaries
            .Include(s => s.User)
            .ToListAsync();
        Assert.Equal(2, savedSummaries.Count);
        Assert.Contains(savedSummaries, s => s.UserId == _testUserId);
        Assert.Contains(savedSummaries, s => s.UserId == otherUserId);

        // Act
        var result = (await _service.GetSummariesAsync()).ToList();

        // Assert
        Assert.Single(result);
        var returnedSummary = result.First();
        Assert.Equal(currentUserSummary.Id, returnedSummary.Id);
        Assert.Equal(_testUserId, returnedSummary.UserId);
        Assert.Equal("Current User Summary", returnedSummary.OriginalText);
        Assert.Equal("Current User", returnedSummary.UserDisplayName);
    }

    [Fact]
    public async Task UpdateSummaryAsync_WhenSummaryNotFound_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateDto = new UpdateSummaryDto
        {
            OriginalText = "Updated Text",
            SummaryText = "Updated Summary"
        };

        // Act
        var result = await _service.UpdateSummaryAsync(nonExistentId, updateDto);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateSummaryAsync_WithFailedReload_ShouldThrowException()
    {
        // Arrange
        var dto = new CreateSummaryDto
        {
            OriginalText = "Test text",
            SummaryText = "Test summary"
        };

        // Create a summary in the database
        var summary = new Summary
        {
            Id = Guid.NewGuid(),
            UserId = _testUserId,
            OriginalText = dto.OriginalText,
            SummaryText = dto.SummaryText,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedAtLocal = DateTime.UtcNow // Test data uses UTC
        };

        _context.Summaries.Add(summary);
        await _context.SaveChangesAsync();

        // Simulate a failed reload by removing the summary from the database
        _context.Summaries.Remove(summary);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateSummaryAsync(dto));
    }

    #region Helper Methods
    private async Task<User> CreateTestUser(string email = "test@test.com", string displayName = "Test User")
    {
        var user = new User
        {
            Id = _testUserId,
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
