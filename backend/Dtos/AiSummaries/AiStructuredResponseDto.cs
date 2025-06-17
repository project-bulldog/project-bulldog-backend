using System.Text.Json.Serialization;

namespace backend.Dtos.AiSummaries
{
    public class AiStructuredResponseDto
    {
        public string Summary { get; set; } = string.Empty;
        public List<ActionItemStructDto> ActionItems { get; set; } = new();
    }

    public class ActionItemStructDto
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("suggested_time")]
        public string? SuggestedTime { get; set; }

        [JsonPropertyName("is_date_only")]
        public bool? IsDateOnly { get; set; }
    }
}
