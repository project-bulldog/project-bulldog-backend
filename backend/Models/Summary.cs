namespace backend.Models
{
    public class Summary
    {
        public int Id { get; set; }   // PK
        public Guid UserId { get; set; }  // FK → User
        public required string OriginalText { get; set; }
        public required string SummaryText { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public required User User { get; set; }
        public ICollection<ActionItem> ActionItems { get; set; } = new List<ActionItem>();
    }
}
