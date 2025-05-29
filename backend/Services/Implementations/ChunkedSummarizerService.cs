using System.Text;
using backend.Services.Interfaces;
using SharpToken;

namespace backend.Services.Implementations
{
    public class ChunkedSummarizerService : IChunkedSummarizerService
    {
        private readonly IOpenAiService _openAiService;
        private const int MaxTokensPerChunk = 400;
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
                return await _openAiService.GetSummaryOnlyAsync(input, model);
            }

            var chunks = ChunkByTokens(input, encoder, MaxTokensPerChunk);
            var summaries = new List<string>();

            Console.WriteLine($"Total input tokens: {totalTokens}");
            Console.WriteLine($"Chunks created: {chunks.Count}");

            foreach (var chunk in chunks)
            {
                summaries.Add(await _openAiService.GetSummaryOnlyAsync(chunk, model));
            }

            if (!useMapReduce)
                return string.Join("\n\n", summaries);

            var stitched = string.Join("\n\n", summaries);
            return await _openAiService.GetSummaryOnlyAsync($"Summarize this combined summary:\n{stitched}", model);
        }

        private static List<string> ChunkByTokens(string text, GptEncoding encoder, int maxTokens)
        {
            var chunks = new List<string>();

            // Normalize escaped newlines (\\n\\n) to actual line breaks
            text = text.Replace("\\n", "\n").Replace("\r\n", "\n");

            // Split by double line breaks to find paragraph chunks
            var paragraphs = text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);

            var current = new StringBuilder();
            int currentTokens = 0;

            foreach (var para in paragraphs)
            {
                int paraTokens = encoder.CountTokens(para);

                if (paraTokens > maxTokens)
                {
                    if (current.Length > 0)
                    {
                        chunks.Add(current.ToString().Trim());
                        current.Clear();
                        currentTokens = 0;
                    }

                    chunks.Add(para.Trim());
                    continue;
                }

                if (currentTokens + paraTokens > maxTokens)
                {
                    if (current.Length > 0)
                    {
                        chunks.Add(current.ToString().Trim());
                        current.Clear();
                        currentTokens = 0;
                    }
                }

                current.AppendLine(para.Trim());
                currentTokens += paraTokens;
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString().Trim());
            }

            return chunks;
        }


    }
}
