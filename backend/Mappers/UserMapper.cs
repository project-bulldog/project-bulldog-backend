using backend.Dtos.Users;
using backend.Models;

namespace backend.Mappers;

public static class UserMapper
{
    public static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Summaries = [.. user.Summaries.Select(SummaryMapper.ToDto)]
    };
}
