namespace backend.Dtos.ActionItems;

public class CreateActionItemDto
{
    public int SummaryId { get; set; }
    public string Text { get; set; } = null!;
    public DateTime? DueAt { get; set; }
}
