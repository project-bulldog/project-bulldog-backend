using backend.Dtos.Summaries;
using backend.Models;

namespace backend.Services.Interfaces;

public interface ISummaryService
{
    Task<IEnumerable<SummaryDto>> GetSummariesAsync();
    Task<SummaryDto?> GetSummaryAsync(Guid id);
    Task<SummaryDto> CreateSummaryAsync(CreateSummaryDto summary);
    Task<bool> UpdateSummaryAsync(Guid id, UpdateSummaryDto updateDto);
    Task<bool> DeleteSummaryAsync(Guid id);
}
