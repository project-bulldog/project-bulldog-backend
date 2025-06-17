using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.AiSummaries;
using backend.Dtos.Summaries;
using backend.Services.Auth.Interfaces;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;

public class AiServiceTests : IDisposable
{
    private readonly BulldogDbContext _context;
    private readonly Mock<IOpenAiService> _openAiServiceMock;
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock;
    private readonly Mock<ILogger<AiService>> _loggerMock;
    private readonly AiService _service;
    private readonly Guid _testUserId = Guid.NewGuid();

    public AiServiceTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BulldogDbContext(options);
        _openAiServiceMock = new Mock<IOpenAiService>();
        _currentUserProviderMock = new Mock<ICurrentUserProvider>();
        _loggerMock = new Mock<ILogger<AiService>>();
        _currentUserProviderMock.Setup(x => x.UserId).Returns(_testUserId);
        // Use a very low chunk threshold for tests
        _service = new AiService(_context, _currentUserProviderMock.Object, _openAiServiceMock.Object, _loggerMock.Object, 10);
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
        var inputText = "Test input text";
        var expectedSummary = "Test summary";
        var expectedActionItems = new List<ActionItemDto>
        {
            new ActionItemDto { Text = "Action 1", IsDone = false, IsDateOnly = true },
            new ActionItemDto { Text = "Action 2", IsDone = false, IsDateOnly = false }
        };

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto(inputText);

        // Act
        var result = await _service.SummarizeAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        Assert.Equal(inputText, result.Summary.OriginalText);
        Assert.Equal(expectedSummary, result.Summary.SummaryText);
        Assert.Equal(_testUserId, result.Summary.UserId);
        Assert.Equal(2, result.Summary.ActionItems.Count);
        Assert.Equal(expectedActionItems[0].Text, result.Summary.ActionItems.First().Text);
        Assert.Equal(expectedActionItems[1].Text, result.Summary.ActionItems.ElementAt(1).Text);
        Assert.Equal(expectedActionItems[0].IsDateOnly, result.Summary.ActionItems.First().IsDateOnly);
        Assert.Equal(expectedActionItems[1].IsDateOnly, result.Summary.ActionItems.ElementAt(1).IsDateOnly);
        Assert.False(result.Summary.ActionItems.First().IsDone);
        Assert.False(result.Summary.ActionItems.ElementAt(1).IsDone);

        // Verify database state
        var savedSummary = await _context.Summaries
            .Include(s => s.ActionItems)
            .FirstOrDefaultAsync(s => s.Id == result.Summary.Id);

        Assert.NotNull(savedSummary);
        Assert.Equal(inputText, savedSummary.OriginalText);
        Assert.Equal(expectedSummary, savedSummary.SummaryText);
        Assert.Equal(2, savedSummary.ActionItems.Count);
        Assert.Equal(expectedActionItems[0].IsDateOnly, savedSummary.ActionItems.First().IsDateOnly);
        Assert.Equal(expectedActionItems[1].IsDateOnly, savedSummary.ActionItems.ElementAt(1).IsDateOnly);
    }

    [Fact]
    public async Task SummarizeTextAsync_WithEmptyActionItems_ShouldCreateSummaryOnly()
    {
        // Arrange
        var inputText = "Test input text";
        var expectedSummary = "Test summary";
        var expectedActionItems = new List<ActionItemDto>();

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto(inputText);

        // Act
        var result = await _service.SummarizeAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        Assert.Equal(inputText, result.Summary.OriginalText);
        Assert.Equal(expectedSummary, result.Summary.SummaryText);
        Assert.Equal(_testUserId, result.Summary.UserId);
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
        var inputText = "Test input text";
        var request = new CreateAiSummaryRequestDto(inputText);

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("OpenAI service error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _service.SummarizeAsync(request));
    }

    [Fact]
    public async Task SummarizeTextAsync_ShouldSetCorrectTimestamps()
    {
        // Arrange
        var inputText = "Test input text";
        var expectedSummary = "Test summary";
        var expectedActionItems = new List<ActionItemDto>
        {
            new ActionItemDto { Text = "Action 1", IsDone = false }
        };

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        var request = new CreateAiSummaryRequestDto(inputText);

        // Act
        var result = await _service.SummarizeAsync(request);

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
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true, "gpt-3.5-turbo");

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
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true, "gpt-3.5-turbo");

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
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, false, "gpt-3.5-turbo");

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
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true, "gpt-3.5-turbo");

        // Mock the SummarizeAndExtractAsync for chunk processing
        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) =>
            {
                if (text.StartsWith("Summarize this combined summary"))
                {
                    return ("Final Summary", new List<ActionItemDto> { new ActionItemDto { Text = "Final Task", IsDone = false } });
                }
                return ("Chunk Summary", new List<ActionItemDto>
                {
                    new ActionItemDto { Text = "Task 1", IsDone = false, IsDateOnly = true },
                    new ActionItemDto { Text = "Task 2", IsDone = false, IsDateOnly = false }
                });
            });

        // Mock the GetSummaryOnlyAsync for final summary stitching
        _openAiServiceMock
            .Setup(x => x.GetSummaryOnlyAsync(It.Is<string>(s => s.StartsWith("Summarize this combined summary")), It.IsAny<string>()))
            .ReturnsAsync("Final Summary");

        // Act
        var result = await _service.SummarizeAndExtractActionItemsChunkedAsync(request);
        var (summary, tasks) = result;

        // Assert
        _openAiServiceMock.Verify(
            x => x.SummarizeAndExtractAsync(
                It.Is<string>(s => !s.StartsWith("Summarize this combined summary")),
                It.IsAny<string>()),
            Times.AtLeast(2));

        Assert.NotNull(summary);
        Assert.Equal("Final Summary", summary);
        Assert.True(tasks.Count >= 4, $"Expected at least 4 tasks, but got {tasks.Count}"); // At least 2 tasks per chunk, at least 2 chunks
        Assert.All(tasks, task =>
        {
            Assert.NotNull(task);
            Assert.NotNull(task.Text);
            Assert.False(task.IsDone);
        });
    }

    [Fact]
    public async Task SummarizeAndExtractActionItemsFromLongTextAsync_ShouldBypassChunking_ForGpt4WithLowTokens()
    {
        // Arrange
        var input = "Short input text.";
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true, "gpt-4-turbo");

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => ("GPT-4 Summary", new List<ActionItemDto> { new ActionItemDto { Text = "Task 1", IsDone = false } }));

        // Act
        var result = await _service.SummarizeAndExtractActionItemsChunkedAsync(request);
        var (summary, tasks) = result;

        // Assert
        _openAiServiceMock.Verify(x => x.SummarizeAndExtractAsync(input, "gpt-4-turbo"), Times.Once);
        Assert.Contains("GPT-4 Summary", summary);
        Assert.Single(tasks);
    }

    [Fact]
    public async Task SummarizeAndExtractActionItemsFromLongTextAsync_ShouldHandleEmptySummaries()
    {
        // Arrange
        var input = string.Join("\n\n", Enumerable.Repeat("Test paragraph", 50));
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true);

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => ("", new List<ActionItemDto> { new ActionItemDto { Text = "Task 1", IsDone = false } }));

        _openAiServiceMock
            .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => null);

        // Act
        var result = await _service.SummarizeAndExtractActionItemsChunkedAsync(request);
        var (summary, tasks) = result;

        // Assert
        Assert.Equal("No summary available", summary);
        Assert.NotEmpty(tasks);
    }

    [Fact]
    public async Task SummarizeAndExtractActionItemsFromLongTextAsync_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var input = "Line 1\\n\\nLine 2\r\n\r\nLine 3";
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true);

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => ("Summary", new List<ActionItemDto> { new ActionItemDto { Text = "Task 1", IsDone = false } }));

        // Act
        var result = await _service.SummarizeAndExtractActionItemsChunkedAsync(request);
        var (summary, tasks) = result;

        // Assert
        Assert.NotNull(summary);
        Assert.NotEmpty(tasks);
        _openAiServiceMock.Verify(x => x.SummarizeAndExtractAsync(It.Is<string>(s => !s.Contains("\\n")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SummarizeAndExtractActionItemsFromLongTextAsync_ShouldHandleVeryLongParagraphs()
    {
        // Arrange
        var longParagraph = string.Join(" ", Enumerable.Repeat("word", 1000));
        var input = $"{longParagraph}\n\n{longParagraph}";
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true);

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string text, string _) => ("Summary", new List<ActionItemDto> { new ActionItemDto { Text = "Task 1", IsDone = false } }));

        // Act
        var result = await _service.SummarizeAndExtractActionItemsChunkedAsync(request);
        var (summary, tasks) = result;

        // Assert
        Assert.NotNull(summary);
        Assert.NotEmpty(tasks);
        _openAiServiceMock.Verify(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task SummarizeAndSaveChunkedAsync_ShouldSaveSummaryAndActionItems()
    {
        // Arrange
        var input = "Test input text";
        var expectedSummary = "Test summary";
        var expectedActionItems = new List<ActionItemDto>
        {
            new ActionItemDto { Text = "Task 1", IsDone = false, IsDateOnly = true },
            new ActionItemDto { Text = "Task 2", IsDone = false, IsDateOnly = false }
        };
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true);

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        // Act
        var result = await _service.SummarizeAndSaveChunkedAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        Assert.Equal(input, result.Summary.OriginalText);
        Assert.Equal(expectedSummary, result.Summary.SummaryText);
        Assert.Equal(_testUserId, result.Summary.UserId);
        Assert.Equal(2, result.Summary.ActionItems.Count);
        Assert.Equal(expectedActionItems[0].Text, result.Summary.ActionItems.First().Text);
        Assert.Equal(expectedActionItems[1].Text, result.Summary.ActionItems.ElementAt(1).Text);
        Assert.Equal(expectedActionItems[0].IsDateOnly, result.Summary.ActionItems.First().IsDateOnly);
        Assert.Equal(expectedActionItems[1].IsDateOnly, result.Summary.ActionItems.ElementAt(1).IsDateOnly);
        Assert.False(result.Summary.ActionItems.First().IsDone);
        Assert.False(result.Summary.ActionItems.ElementAt(1).IsDone);

        // Verify database state
        var savedSummary = await _context.Summaries
            .Include(s => s.ActionItems)
            .FirstOrDefaultAsync(s => s.Id == result.Summary.Id);

        Assert.NotNull(savedSummary);
        Assert.Equal(input, savedSummary.OriginalText);
        Assert.Equal(expectedSummary, savedSummary.SummaryText);
        Assert.Equal(2, savedSummary.ActionItems.Count);
        Assert.Equal(expectedActionItems[0].IsDateOnly, savedSummary.ActionItems.First().IsDateOnly);
        Assert.Equal(expectedActionItems[1].IsDateOnly, savedSummary.ActionItems.ElementAt(1).IsDateOnly);
    }

    [Fact]
    public async Task SummarizeAndSaveChunkedAsync_ShouldHandleEmptyActionItems()
    {
        // Arrange
        var input = "Test input text";
        var expectedSummary = "Test summary";
        var expectedActionItems = new List<ActionItemDto>();
        var request = new AiChunkedSummaryResponseDto(input, _testUserId, true);

        _openAiServiceMock
            .Setup(x => x.SummarizeAndExtractAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((expectedSummary, expectedActionItems));

        // Act
        var result = await _service.SummarizeAndSaveChunkedAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        Assert.Equal(input, result.Summary.OriginalText);
        Assert.Equal(expectedSummary, result.Summary.SummaryText);
        Assert.Equal(_testUserId, result.Summary.UserId);
        Assert.Empty(result.Summary.ActionItems);

        // Verify database state
        var savedSummary = await _context.Summaries
            .Include(s => s.ActionItems)
            .FirstOrDefaultAsync(s => s.Id == result.Summary.Id);

        Assert.NotNull(savedSummary);
        Assert.Equal(input, savedSummary.OriginalText);
        Assert.Equal(expectedSummary, savedSummary.SummaryText);
        Assert.Empty(savedSummary.ActionItems);
    }
}
