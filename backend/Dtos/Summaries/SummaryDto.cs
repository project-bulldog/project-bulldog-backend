using backend.Dtos.ActionItems;

namespace backend.Dtos.Summaries;

public class SummaryDto
{
    public int Id { get; set; }
    public string OriginalText { get; set; } = null!;
    public string SummaryText { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = null!;

    public List<ActionItemDto> ActionItems { get; set; } = [];
}
