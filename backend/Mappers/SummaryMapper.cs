using backend.Dtos.ActionItems;
using backend.Dtos.Summaries;
using backend.Models;

namespace backend.Mappers;

public static class SummaryMapper
{
    public static SummaryDto ToDto(Summary summary)
    {
        return new SummaryDto
        {
            Id = summary.Id,
            OriginalText = summary.OriginalText,
            SummaryText = summary.SummaryText,
            CreatedAt = summary.CreatedAt,
            UserId = summary.UserId,
            UserDisplayName = summary.User?.DisplayName ?? "[Unknown]",
            ActionItems = ActionItemMapper.ToDtoList(summary.ActionItems)
        };
    }
}
