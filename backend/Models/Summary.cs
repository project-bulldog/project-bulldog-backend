using System.Text.Json.Serialization;

namespace backend.Models
{
    public class Summary
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public required string OriginalText { get; set; }
        public required string SummaryText { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public User? User { get; set; }
        public ICollection<ActionItem> ActionItems { get; set; } = [];
    }
}
