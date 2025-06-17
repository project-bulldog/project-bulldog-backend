using backend.Dtos.ActionItems;

namespace backend.Services.Interfaces;

public interface IOpenAiService
{
    Task<(string summary, List<ActionItemDto> actionItems)> SummarizeAndExtractAsync(string input, string? modelOverride = null);
    Task<string> GetSummaryOnlyAsync(string input, string? modelOverride = null);
}
