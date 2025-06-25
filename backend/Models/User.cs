using backend.Models.Auth;

namespace backend.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        //Email verification
        public bool EmailVerified { get; set; } = false;

        //2FA
        public string? PhoneNumber { get; set; }
        public bool PhoneNumberVerified { get; set; } = false;
        public bool TwoFactorEnabled { get; set; }
        public string? CurrentOtp { get; set; }
        public int OtpAttemptsLeft { get; set; } = 5;
        public DateTime? OtpExpiresAt { get; set; }

        //User preferences
        public string? TimeZoneId { get; set; }

        public ICollection<Summary> Summaries { get; set; } = [];
        public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    }
}
