namespace backend.Dtos.Summaries;

public class CreateSummaryDto
{
    public string OriginalText { get; set; } = null!;
    public string SummaryText { get; set; } = null!;
}
