namespace backend.Dtos;

public class ActionItemDto
{
    public int Id { get; set; }
    public string Text { get; set; } = null!;
    public bool IsDone { get; set; }
    public DateTime? DueAt { get; set; }
}
