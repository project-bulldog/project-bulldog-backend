using System.Text;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Moq;
using SharpToken;
using Xunit;

namespace backend.Tests.Services
{
    public class ChunkedSummarizerServiceTests
    {
        private readonly Mock<IOpenAiService> _openAiServiceMock;
        private readonly ChunkedSummarizerService _service;

        public ChunkedSummarizerServiceTests()
        {
            _openAiServiceMock = new Mock<IOpenAiService>();
            _service = new ChunkedSummarizerService(_openAiServiceMock.Object);
        }

        [Fact]
        public async Task SummarizeLongTextAsync_ShouldChunk_WhenTokenLimitExceeded()
        {
            // Arrange
            var input = string.Join("\\n\\n", Enumerable.Repeat("This is a long paragraph.", 50)); // forces chunking

            _openAiServiceMock
                .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string text, string _) => $"Summary: [{text.Length}]");

            // Act
            var result = await _service.SummarizeLongTextAsync(input);

            // Assert
            _openAiServiceMock.Verify(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), "gpt-3.5-turbo"), Times.AtLeast(2));
            Assert.Contains("Summary:", result);
        }

        [Fact]
        public async Task SummarizeLongTextAsync_ShouldBypassChunking_ForGpt4WithLowTokens()
        {
            // Arrange
            var input = "Short input text.";
            _openAiServiceMock
                .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string text, string _) => $"GPT-4 Summary: {text}");

            // Act
            var result = await _service.SummarizeLongTextAsync(input, modelOverride: "gpt-4-turbo");

            // Assert
            _openAiServiceMock.Verify(x => x.GetSummaryOnlyAsync(input, "gpt-4-turbo"), Times.Once);
            Assert.Contains("GPT-4 Summary:", result);
        }

        [Fact]
        public async Task SummarizeLongTextAsync_ShouldReturnStitchedSummary_WhenMapReduceIsEnabled()
        {
            // Arrange
            var input = string.Join("\\n\\n", Enumerable.Repeat("Paragraph content.", 30)); // long input

            _openAiServiceMock
                .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string text, string _) => $"SummaryChunk");

            // Act
            var result = await _service.SummarizeLongTextAsync(input, useMapReduce: true);

            // Assert
            _openAiServiceMock.Verify(x => x.GetSummaryOnlyAsync(It.Is<string>(s => s.StartsWith("Summarize this combined summary")), "gpt-3.5-turbo"), Times.Once);
            Assert.Contains("SummaryChunk", result);
        }

        [Fact]
        public async Task SummarizeLongTextAsync_ShouldReturnJoinedSummaries_WhenMapReduceDisabled()
        {
            // Arrange: force >1000 tokens
            var input = string.Join("\n\n", Enumerable.Repeat("This is a long paragraph filled with many different kinds of words and phrases designed to ensure that the tokenizer splits it into multiple tokens reliably. We are testing the chunking mechanism of the summarizer service here. Let's make sure it works as expected.", 200));

            _openAiServiceMock
                .Setup(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string text, string _) => $"Summary: {text.Substring(0, Math.Min(5, text.Length))}");

            // Act
            var result = await _service.SummarizeLongTextAsync(input, useMapReduce: false);

            // Assert
            _openAiServiceMock.Verify(x => x.GetSummaryOnlyAsync(It.IsAny<string>(), "gpt-3.5-turbo"), Times.AtLeast(2));
            Assert.Contains("Summary:", result);
        }

    }
}
