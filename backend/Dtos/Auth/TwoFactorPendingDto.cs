namespace backend.Dtos.Auth;

public class TwoFactorPendingDto
{
    public Guid UserId { get; set; }
    public string Message { get; set; } = "Two-factor verification required.";
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool CanUseEmail { get; set; } = true;
    public bool CanUseSms { get; set; } = true;
}
