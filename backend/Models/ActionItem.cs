namespace backend.Models
{
    public class ActionItem
    {
        public Guid Id { get; init; }
        public Guid SummaryId { get; init; }
        public required string Text { get; set; }
        public bool IsDone { get; set; } = false;
        public DateTime? DueAt { get; set; }
        public bool IsDateOnly { get; set; } = false;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public bool ShouldRemind { get; set; } = true;
        public int? ReminderMinutesBeforeDue { get; set; }

        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
        public DateTime CreatedAtLocal { get; init; }

        public Summary? Summary { get; set; }
    }
}
