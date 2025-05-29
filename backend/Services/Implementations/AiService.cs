using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.AiSummaries;
using backend.Dtos.Summaries;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class AiService : IAiService
{
    private readonly BulldogDbContext _context;
    private readonly IOpenAiService _openAiService;

    public AiService(BulldogDbContext context, IOpenAiService openAiService)
    {
        _context = context;
        _openAiService = openAiService;
    }

    public async Task<AiSummaryResponseDto> SummarizeTextAsync(CreateAiSummaryRequestDto request, Guid userId)
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
}
