namespace backend.Dtos.AiSummaries;
using System.Collections.Generic;
using backend.Dtos.ActionItems;

public record AiSummaryWithTasksResponseDto(
    string Summary,
    List<ActionItemDto> ActionItems,
    string? UsedTimeZoneId = null
);

