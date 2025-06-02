namespace backend.Dtos.Auth;

public class SessionMetadataDto
{
    public Guid TokenId { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
