using System.ComponentModel.DataAnnotations;

namespace backend.Dtos.Users;

public class UpdateUserDto
{
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
}
