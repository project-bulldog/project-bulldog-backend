using backend.Dtos.Summaries;

namespace backend.Dtos.Users
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        public bool EmailVerified { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneNumberVerified { get; set; }

        public List<SummaryDto> Summaries { get; set; } = new();
    }
}
