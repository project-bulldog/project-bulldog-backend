namespace backend.Dtos.Auth;

public class TwoFactorVerifyRequestDto
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string VerificationMethod { get; set; } = "sms"; // "sms" or "email"
}
