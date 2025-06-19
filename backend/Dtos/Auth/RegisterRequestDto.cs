namespace backend.Dtos.Auth;
public class RegisterRequestDto
{
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Password { get; set; } = null!;
}
