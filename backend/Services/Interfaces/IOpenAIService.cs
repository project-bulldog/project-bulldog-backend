using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Services.Interfaces;

public interface IOpenAiService
{
    Task<(string summary, List<string> tasks)> SummarizeAndExtractAsync(string input, string modelOverride = null);
    Task<string> GetSummaryOnlyAsync(string input, string modelOverride = null);
}
