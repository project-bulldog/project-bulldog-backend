using System.Text.Json.Serialization;

namespace backend.Models
{
    public class Summary
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
        public required string OriginalText { get; set; }
        public required string SummaryText { get; set; }
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
        public DateTime CreatedAtLocal { get; init; }

        // Navigation
        public User User { get; set; } = null!;
        public ICollection<ActionItem> ActionItems { get; set; } = [];
    }
}
