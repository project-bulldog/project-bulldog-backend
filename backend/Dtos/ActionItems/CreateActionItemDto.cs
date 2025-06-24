namespace backend.Dtos.ActionItems;

public class CreateActionItemDto
{
    public Guid? SummaryId { get; set; }
    public string Text { get; set; } = null!;
    public DateTime? DueAt { get; set; }
    public bool IsDateOnly { get; set; } = false;
    public bool ShouldRemind { get; set; } = true;
    public int? ReminderMinutesBeforeDue { get; set; }

}
