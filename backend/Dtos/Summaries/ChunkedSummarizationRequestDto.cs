namespace backend.Dtos.Summaries;
public record ChunkedSummaryRequestDto(string Input, Guid UserId, bool? UseMapReduce = true, string? Model = null);
