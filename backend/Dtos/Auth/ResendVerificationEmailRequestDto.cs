using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Auth
{
    public class ResendVerificationEmailRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
