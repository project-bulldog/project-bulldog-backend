namespace backend.Dtos;

public class SummaryDto
{
    public int Id { get; set; }
    public string OriginalText { get; set; } = null!;
    public string SummaryText { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public List<ActionItemDto> ActionItems { get; set; } = new();
}
