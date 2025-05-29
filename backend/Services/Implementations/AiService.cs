using System.Text;
using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.AiSummaries;
using backend.Dtos.Summaries;
using backend.Models;
using backend.Services.Interfaces;
using SharpToken;

namespace backend.Services.Implementations;

public class AiService : IAiService
{
    private readonly BulldogDbContext _context;
    private readonly IOpenAiService _openAiService;
    private const int MaxTokensPerChunk = 400;
    private const string DefaultModel = "gpt-3.5-turbo";

    public AiService(BulldogDbContext context, IOpenAiService openAiService)
    {
        _context = context;
        _openAiService = openAiService;
    }

    public async Task<AiSummaryResponseDto> SummarizeAsync(CreateAiSummaryRequestDto request, Guid userId)
    {
        // 🔥 Call OpenAI with the input text
        var (summaryText, actionItemTexts) = await _openAiService.SummarizeAndExtractAsync(request.InputText);

        var summary = new Summary
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            OriginalText = request.InputText,
            SummaryText = summaryText
        };

        var actionItems = actionItemTexts.Select(text => new ActionItem
        {
            Id = Guid.NewGuid(),
            SummaryId = summary.Id,
            Text = text,
            IsDone = false
        }).ToList();

        await _context.Summaries.AddAsync(summary);
        await _context.ActionItems.AddRangeAsync(actionItems);
        await _context.SaveChangesAsync();

        return new AiSummaryResponseDto
        {
            Summary = new SummaryDto
            {
                Id = summary.Id,
                OriginalText = summary.OriginalText,
                SummaryText = summary.SummaryText,
                CreatedAt = summary.CreatedAt,
                UserId = summary.UserId,
                ActionItems = actionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                }).ToList()
            }
        };
    }

    #region Chunking methods
    public async Task<string> SummarizeChunkedAsync(ChunkedSummaryRequestDto request)
    {
        string model = request.Model ?? DefaultModel;
        bool useMapReduce = request.UseMapReduce ?? true;

        // If model is GPT-4-turbo and input is within token limit, skip chunking
        var encoder = GptEncoding.GetEncodingForModel(model);
        var totalTokens = encoder.CountTokens(request.Input);

        if (model == "gpt-4-turbo" && totalTokens < 120_000)
        {
            return await _openAiService.GetSummaryOnlyAsync(request.Input, model);
        }

        var chunks = ChunkByTokens(request.Input, encoder, MaxTokensPerChunk);
        var summaries = new List<string>();

        Console.WriteLine($"Total input tokens: {totalTokens}");
        Console.WriteLine($"Chunks created: {chunks.Count}");

        foreach (var chunk in chunks)
        {
            summaries.Add(await _openAiService.GetSummaryOnlyAsync(chunk, model));
        }

        if (!useMapReduce)
            return string.Join("\n\n", summaries);

        var stitchedSummaryInput = string.Join("\n\n", summaries);
        return await _openAiService.GetSummaryOnlyAsync($"Summarize this combined summary:\n{stitchedSummaryInput}", model);
    }

    public async Task<(string summary, List<string> tasks)> SummarizeAndExtractActionItemsChunkedAsync(ChunkedSummaryRequestDto request)
    {
        string model = request.Model ?? DefaultModel;
        bool useMapReduce = request.UseMapReduce ?? true;

        var encoder = GptEncoding.GetEncodingForModel(model);
        var totalTokens = encoder.CountTokens(request.Input);

        if (model == "gpt-4-turbo" && totalTokens < 120_000)
        {
            return await _openAiService.SummarizeAndExtractAsync(request.Input, model);
        }

        var chunks = ChunkByTokens(request.Input, encoder, MaxTokensPerChunk);
        var summaries = new List<string>();
        var allTasks = new List<string>();

        Console.WriteLine($"[Extract] Total tokens: {totalTokens}");
        Console.WriteLine($"[Extract] Chunk count: {chunks.Count}");

        foreach (var chunk in chunks)
        {
            var (summary, tasks) = await _openAiService.SummarizeAndExtractAsync(chunk, model);
            if (!string.IsNullOrEmpty(summary))
            {
                summaries.Add(summary);
            }
            allTasks.AddRange(tasks);
        }

        if (!useMapReduce || summaries.Count == 0)
            return (string.Join("\n\n", summaries), allTasks);

        var stitchedSummaryInput = string.Join("\n\n", summaries);
        var finalSummary = await _openAiService.GetSummaryOnlyAsync($"Summarize this combined summary:\n{stitchedSummaryInput}", model);

        return (finalSummary ?? "No summary available", allTasks);
    }
    #endregion

    #region Private Methods
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
    #endregion
}
