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

    public AiService(BulldogDbContext context)
    {
        _context = context;
    }

    public async Task<AiSummaryResponseDto> SummarizeTextAsync(CreateAiSummaryRequestDto request, Guid userId)
    {
        // TODO: Call OpenAI API here. For now, mock result.
        var summary = new Summary
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            OriginalText = request.InputText,
            SummaryText = "Mock summary: this is a brief overview of the input text."
        };

        var actionItems = new List<ActionItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SummaryId = summary.Id,
                Text = "Mock task 1 from AI",
                IsDone = false,
                DueAt = DateTime.UtcNow.AddDays(3)
            },
            new()
            {
                Id = Guid.NewGuid(),
                SummaryId = summary.Id,
                Text = "Mock task 2 from AI",
                IsDone = false
            }
        };

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
                ActionItems = [.. actionItems.Select(ai => new ActionItemDto
                {
                    Id = ai.Id,
                    Text = ai.Text,
                    IsDone = ai.IsDone,
                    DueAt = ai.DueAt
                })]
            }
        };
    }
}
