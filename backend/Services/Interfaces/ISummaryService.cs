using backend.Dtos.Summaries;
using backend.Models;

namespace backend.Services.Interfaces;

public interface ISummaryService
{
    Task<IEnumerable<SummaryDto>> GetSummariesAsync();
    Task<SummaryDto?> GetSummaryAsync(int id);
    Task<SummaryDto> CreateSummaryAsync(Summary summary);
    Task<bool> UpdateSummaryAsync(int id, Summary summary);
    Task<bool> DeleteSummaryAsync(int id);
}
