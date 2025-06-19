namespace backend.Dtos.Auth;

public class VerifyPhoneRequestDto
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
}
