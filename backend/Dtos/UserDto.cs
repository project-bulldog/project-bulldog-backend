namespace backend.Dtos;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    public List<SummaryDto> Summaries { get; set; } = new();
}
