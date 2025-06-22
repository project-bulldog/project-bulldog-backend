namespace backend.Dtos.Auth;

public class VerifyEmailRequestDto
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
}
