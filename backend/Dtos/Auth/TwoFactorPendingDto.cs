namespace backend.Dtos.Auth;

public class TwoFactorPendingDto
{
    public Guid UserId { get; set; }
    public string Message { get; set; } = "Two-factor verification required.";
}
