namespace backend.Dtos.Auth;

public class RequestTwoFactorDto
{
    public Guid UserId { get; set; }
    public string Method { get; set; } = "sms"; // "sms" or "email"
}
