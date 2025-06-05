namespace backend.Dtos.AiSummaries;
public record AiChunkedSummaryResponseDto(string Input, Guid UserId, bool? UseMapReduce = true, string? Model = null);
