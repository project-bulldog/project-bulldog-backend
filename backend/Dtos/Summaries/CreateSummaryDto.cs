namespace backend.Dtos.Summaries;

using System.Collections.Generic;
using backend.Dtos.ActionItems;

public class CreateSummaryDto
{
    public string OriginalText { get; set; } = null!;
    public string SummaryText { get; set; } = null!;
    public List<CreateActionItemDto>? ActionItems { get; set; }
}
