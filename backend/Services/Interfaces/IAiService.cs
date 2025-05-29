using backend.Dtos.AiSummaries;

namespace backend.Services.Interfaces;
public interface IAiService
{
    Task<AiSummaryResponseDto> SummarizeTextAsync(CreateAiSummaryRequestDto request, Guid userId);
}


