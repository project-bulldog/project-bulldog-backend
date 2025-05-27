using backend.Dtos.Summaries;

namespace backend.Dtos.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    public List<SummaryDto> Summaries { get; set; } = new();
}
