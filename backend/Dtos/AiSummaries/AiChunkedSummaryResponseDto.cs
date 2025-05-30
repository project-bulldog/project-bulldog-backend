namespace backend.Dtos.Summaries;
public record AiChunkedSummaryResponseDto(string Input, Guid UserId, bool? UseMapReduce = true, string? Model = null);
