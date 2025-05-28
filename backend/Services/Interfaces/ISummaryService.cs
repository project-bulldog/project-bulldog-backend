using backend.Dtos.Summaries;
using backend.Models;

namespace backend.Services.Interfaces;

public interface ISummaryService
{
    Task<IEnumerable<SummaryDto>> GetSummariesAsync();
    Task<SummaryDto?> GetSummaryAsync(int id);
    Task<SummaryDto> CreateSummaryAsync(CreateSummaryDto summary);
    Task<bool> UpdateSummaryAsync(int id, UpdateSummaryDto updateDto);
    Task<bool> DeleteSummaryAsync(int id);
}
