namespace backend.Models
{
    public class ActionItem
    {
        public int Id { get; set; }    // PK
        public int SummaryId { get; set; }   // FK → Summary
        public required string Text { get; set; }
        public bool IsDone { get; set; } = false;
        public DateTime? DueAt { get; set; }

        // Navigation
        public required Summary Summary { get; set; }
    }
}
