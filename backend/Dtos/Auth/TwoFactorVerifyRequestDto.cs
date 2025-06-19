namespace backend.Dtos.Auth;

public class TwoFactorVerifyRequestDto
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
}
