using backend.Dtos.AiSummaries;

namespace backend.Services.Interfaces;
public interface IAiService
{
    // Summarize short or mid-sized input (e.g. emails, notes)
    Task<AiSummaryResponseDto> SummarizeAsync(CreateAiSummaryRequestDto request);

    // Summarize long-form text (with chunking + optional map-reduce)
    Task<string> SummarizeChunkedAsync(AiChunkedSummaryResponseDto request);

    // Summarize long-form text and extract action items (with chunking + optional map-reduce)
    Task<(string summary, List<string> actionItems)> SummarizeAndExtractActionItemsChunkedAsync(AiChunkedSummaryResponseDto request);
}


