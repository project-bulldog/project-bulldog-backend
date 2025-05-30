using backend.Data;
using backend.Dtos.AiSummaries;
using backend.Dtos.Summaries;
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
    private readonly Guid _testUserId = Guid.NewGuid();

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
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto { InputText = inputText };

        // Act
        var result = await _service.SummarizeAsync(request, userId);

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
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto { InputText = inputText };

        // Act
        var result = await _service.SummarizeAsync(request, userId);

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
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("OpenAI service error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _service.SummarizeAsync(request, userId));
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
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto { InputText = inputText };

        // Act
        var result = await _service.SummarizeAsync(request, userId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        Assert.NotEqual(default, result.Summary.CreatedAt);
        Assert.True(result.Summary.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.Summary.CreatedAt >= DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task SummarizeLongTextAsync_ShouldChunk_WhenTokenLimitExceeded()
    {
        // Arrange
        var input = string.Join("\n\n", Enumerable.Repeat("This is a long paragraph.", 50));
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true);

        _openAiServiceMock
            .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => $"Summary: [{text.Length}]");

        // Act
        var result = await _service.SummarizeChunkedAsync(request);

        // Assert
        _openAiServiceMock.Verify(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), "gpt-3.5-turbo"), Times.AtLeast(2));
        Assert.Contains("Summary:", result);
    }

    [Fact]
    public async Task SummarizeLongTextAsync_ShouldBypassChunking_ForGpt4WithLowTokens()
    {
        // Arrange
        var input = "Short input text.";
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true, "gpt-4-turbo");

        _openAiServiceMock
            .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => $"GPT-4 Summary: {text}");

        // Act
        var result = await _service.SummarizeChunkedAsync(request);

        // Assert
        _openAiServiceMock.Verify(x => x.GetSummaryOnlyAsync(input, "gpt-4-turbo"), Times.Once);
        Assert.Contains("GPT-4 Summary:", result);
    }

    [Fact]
    public async Task SummarizeLongTextAsync_ShouldReturnStitchedSummary_WhenMapReduceIsEnabled()
    {
        // Arrange
        var input = string.Join("\n\n", Enumerable.Repeat("Paragraph content.", 30));
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true);

        _openAiServiceMock
            .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => $"SummaryChunk");

        // Act
        var result = await _service.SummarizeChunkedAsync(request);

        // Assert
        _openAiServiceMock.Verify(x => x.GetSummaryOnlyAsync(It.Is<string>(s => s.StartsWith("Summarize this combined summary")), "gpt-3.5-turbo"), Times.Once);
        Assert.Contains("SummaryChunk", result);
    }

    [Fact]
    public async Task SummarizeLongTextAsync_ShouldReturnJoinedSummaries_WhenMapReduceDisabled()
    {
        // Arrange
        var input = string.Join("\n\n", Enumerable.Repeat("This is a long paragraph filled with many different kinds of words and phrases designed to ensure that the tokenizer splits it into multiple tokens reliably. We are testing the chunking mechanism of the summarizer service here. Let's make sure it works as expected.", 200));
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, false);

        _openAiServiceMock
            .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => $"Summary: {text.Substring(0, Math.Min(5, text.Length))}");

        // Act
        var result = await _service.SummarizeChunkedAsync(request);

        // Assert
        _openAiServiceMock.Verify(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), "gpt-3.5-turbo"), Times.AtLeast(2));
        Assert.Contains("Summary:", result);
    }

    [Fact]
    public async Task SummarizeAndExtractActionItemsFromLongTextAsync_ShouldChunk_WhenTokenLimitExceeded()
    {
        // Arrange
        var input = string.Join("\n\n", Enumerable.Repeat("This is a long paragraph filled with many different kinds of words and phrases designed to ensure that the tokenizer splits it into multiple tokens reliably. We are testing the chunking mechanism of the summarizer service here. Let's make sure it works as expected.", 50));
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true);

        // Mock the SummarizeAndExtractAsync for chunk processing
        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.Is<string>(s => !s.StartsWith("Summarize this combined summary")), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => ("Chunk Summary", new List<string> { "Task 1", "Task 2" }));

        // Mock the GetSummaryOnlyAsync for final summary stitching
        _openAiServiceMock
            .Setup(x => x.GetSummaryOnlyAsync(It.Is<string>(s => s.StartsWith("Summarize this combined summary")), It.IsAny<string>()))
            .ReturnsAsync("Final Summary");

        // Act
        var result = await _service.SummarizeAndExtractActionItemsChunkedAsync(request);
        var (summary, tasks) = result;

        // Assert
        _openAiServiceMock.Verify(x => x.SummarizeAndExtractAsync(It.Is<string>(s => !s.StartsWith("Summarize this combined summary")), "gpt-3.5-turbo"), Times.AtLeast(2));
        Assert.NotNull(summary);
        Assert.Equal("Final Summary", summary);
        Assert.True(tasks.Count >= 4, $"Expected at least 4 tasks, but got {tasks.Count}"); // At least 2 tasks per chunk, at least 2 chunks
        Assert.All(tasks, task => Assert.NotNull(task));
    }

    [Fact]
    public async Task SummarizeAndExtractActionItemsFromLongTextAsync_ShouldBypassChunking_ForGpt4WithLowTokens()
    {
        // Arrange
        var input = "Short input text.";
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true, "gpt-4-turbo");

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => ("GPT-4 Summary", new List<string> { "Task 1" }));

        // Act
        var result = await _service.SummarizeAndExtractActionItemsChunkedAsync(request);
        var (summary, tasks) = result;

        // Assert
        _openAiServiceMock.Verify(x => x.SummarizeAndExtractAsync(input, "gpt-4-turbo"), Times.Once);
        Assert.Contains("GPT-4 Summary", summary);
        Assert.Single(tasks);
    }
}
