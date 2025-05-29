using backend.Services.Interfaces;
using SharpToken;

public class ChunkedSummarizerService : IChunkedSummarizerService
{
    private readonly IOpenAiService _openAiService;
    private const int MaxTokensPerChunk = 1500;
    private const string DefaultModel = "gpt-3.5-turbo";

    public ChunkedSummarizerService(IOpenAiService openAiService)
    {
        _openAiService = openAiService;
    }

    public async Task<string> SummarizeLongTextAsync(string input, bool useMapReduce = true, string modelOverride = null)
    {
        string model = modelOverride ?? DefaultModel;

        // If model is GPT-4-turbo and input is within token limit, skip chunking
        var encoder = GptEncoding.GetEncodingForModel(model);
        var totalTokens = encoder.CountTokens(input);

        if (model == "gpt-4-turbo" && totalTokens < 120_000)
        {
            return await _openAiService.GetSummaryAsync(input, model);
        }

        var chunks = ChunkByTokens(input, encoder, MaxTokensPerChunk);
        var summaries = new List<string>();

        foreach (var chunk in chunks)
        {
            summaries.Add(await _openAiService.GetSummaryAsync(chunk, model));
        }

        if (!useMapReduce)
            return string.Join("\n\n", summaries);

        var stitched = string.Join("\n\n", summaries);
        return await _openAiService.GetSummaryAsync($"Summarize this combined summary:\n{stitched}", model);
    }

    private List<string> ChunkByTokens(string text, GptEncoding encoder, int maxTokens)
    {
        var chunks = new List<string>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var current = new StringBuilder();
        int currentTokens = 0;

        foreach (var para in paragraphs)
        {
            int paraTokens = encoder.CountTokens(para);
            if (currentTokens + paraTokens > maxTokens)
            {
                chunks.Add(current.ToString().Trim());
                current.Clear();
                currentTokens = 0;
            }

            current.AppendLine(para.Trim());
            currentTokens += paraTokens;
        }

        if (current.Length > 0)
            chunks.Add(current.ToString().Trim());

        return chunks;
    }
}
