using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Users
{
    public class UpdateUserDto
    {
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
        public bool? EmailVerified { get; set; }
        public bool? PhoneNumberVerified { get; set; }
    }
}
