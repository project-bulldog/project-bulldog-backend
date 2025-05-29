using backend.Data;
using backend.Dtos.AiSummaries;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace backend.Tests.Services;

public class AiServiceTests : IDisposable
{
    private readonly BulldogDbContext _context;
    private readonly Mock<IOpenAiService> _openAiServiceMock;
    private readonly AiService _service;

    public AiServiceTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BulldogDbContext(options);
        _openAiServiceMock = new Mock<IOpenAiService>();
        _service = new AiService(_context, _openAiServiceMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task SummarizeTextAsync_ShouldCreateSummaryAndActionItems()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var inputText = "Test input text";
        var expectedSummary = "Test summary";
        var expectedActionItems = new List<string> { "Action 1", "Action 2" };

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(inputText))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto { InputText = inputText };

        // Act
        var result = await _service.SummarizeTextAsync(request, userId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        Assert.Equal(inputText, result.Summary.OriginalText);
        Assert.Equal(expectedSummary, result.Summary.SummaryText);
        Assert.Equal(userId, result.Summary.UserId);
        Assert.Equal(2, result.Summary.ActionItems.Count);
        Assert.Equal(expectedActionItems[0], result.Summary.ActionItems[0].Text);
        Assert.Equal(expectedActionItems[1], result.Summary.ActionItems[1].Text);
        Assert.False(result.Summary.ActionItems[0].IsDone);
        Assert.False(result.Summary.ActionItems[1].IsDone);

        // Verify database state
        var savedSummary = await _context.Summaries
            .Include(s => s.ActionItems)
            .FirstOrDefaultAsync(s => s.Id == result.Summary.Id);

        Assert.NotNull(savedSummary);
        Assert.Equal(inputText, savedSummary.OriginalText);
        Assert.Equal(expectedSummary, savedSummary.SummaryText);
        Assert.Equal(2, savedSummary.ActionItems.Count);
    }

    [Fact]
    public async Task SummarizeTextAsync_WithEmptyActionItems_ShouldCreateSummaryOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var inputText = "Test input text";
        var expectedSummary = "Test summary";
        var expectedActionItems = new List<string>();

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(inputText))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto { InputText = inputText };

        // Act
        var result = await _service.SummarizeTextAsync(request, userId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        Assert.Equal(inputText, result.Summary.OriginalText);
        Assert.Equal(expectedSummary, result.Summary.SummaryText);
        Assert.Equal(userId, result.Summary.UserId);
        Assert.Empty(result.Summary.ActionItems);

        // Verify database state
        var savedSummary = await _context.Summaries
            .Include(s => s.ActionItems)
            .FirstOrDefaultAsync(s => s.Id == result.Summary.Id);

        Assert.NotNull(savedSummary);
        Assert.Equal(inputText, savedSummary.OriginalText);
        Assert.Equal(expectedSummary, savedSummary.SummaryText);
        Assert.Empty(savedSummary.ActionItems);
    }

    [Fact]
    public async Task SummarizeTextAsync_WhenOpenAiServiceThrows_ShouldPropagateException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var inputText = "Test input text";
        var request = new CreateAiSummaryRequestDto { InputText = inputText };

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(inputText))
            .ThrowsAsync(new Exception("OpenAI service error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _service.SummarizeTextAsync(request, userId));
    }

    [Fact]
    public async Task SummarizeTextAsync_ShouldSetCorrectTimestamps()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var inputText = "Test input text";
        var expectedSummary = "Test summary";
        var expectedActionItems = new List<string> { "Action 1" };

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(inputText))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto { InputText = inputText };

        // Act
        var result = await _service.SummarizeTextAsync(request, userId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        Assert.NotEqual(default, result.Summary.CreatedAt);
        Assert.True(result.Summary.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.Summary.CreatedAt >= DateTime.UtcNow.AddMinutes(-1));
    }
}
