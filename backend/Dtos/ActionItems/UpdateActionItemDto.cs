namespace backend.Dtos.ActionItems;

public class UpdateActionItemDto
{
    public string Text { get; set; } = null!;
    public bool IsDone { get; set; }
    public DateTime? DueAt { get; set; }
    public bool IsDateOnly { get; set; } = false;
}
