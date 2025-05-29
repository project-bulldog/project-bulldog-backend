using backend.Dtos.AiSummaries;
using backend.Dtos.Summaries;

namespace backend.Services.Interfaces;
public interface IAiService
{
    // Summarize short or mid-sized input (e.g. emails, notes)
    Task<AiSummaryResponseDto> SummarizeAsync(CreateAiSummaryRequestDto request, Guid userId);

    // Summarize long-form text (with chunking + optional map-reduce)
    Task<string> SummarizeChunkedAsync(ChunkedSummaryRequestDto request);

    // Summarize long-form text and extract action items (with chunking + optional map-reduce)
    Task<(string summary, List<string> tasks)> SummarizeAndExtractActionItemsChunkedAsync(ChunkedSummaryRequestDto request);
}


