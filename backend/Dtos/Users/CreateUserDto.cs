namespace backend.Dtos.Users
{
    public class CreateUserDto
    {
        public string Email { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string Password { get; set; } = null!;
        public bool EnableTwoFactor { get; set; } = false;
    }
}
