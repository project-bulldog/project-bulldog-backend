namespace backend.Services.Interfaces;

public interface IOpenAiService
{
    Task<(string summary, List<string> tasks)> SummarizeAndExtractAsync(string input);
}
