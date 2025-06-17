namespace backend.Dtos.ActionItems;

public class ActionItemDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = null!;
    public bool IsDone { get; set; }
    public DateTime? DueAt { get; set; }
    public bool IsDateOnly { get; set; }
    public Guid SummaryId { get; set; }
}
