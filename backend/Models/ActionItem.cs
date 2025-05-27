using System.Text.Json.Serialization;

namespace backend.Models
{
    public class ActionItem
    {
        public int Id { get; set; }
        public int SummaryId { get; set; }
        public required string Text { get; set; }
        public bool IsDone { get; set; } = false;
        public DateTime? DueAt { get; set; }

        // Navigation
        [JsonIgnore]
        public Summary? Summary { get; set; }
    }
}
