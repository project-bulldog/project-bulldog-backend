using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Users;
public class CreateUserDto
{
    public string Email { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
}
