namespace backend.Services.Interfaces;

public interface IChunkedSummarizerService
{
    Task<string> SummarizeLongTextAsync(string input, bool useMapReduce = true, string modelOverride = null);
}
