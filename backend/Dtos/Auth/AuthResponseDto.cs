using System.Text.Json.Serialization;
using backend.Dtos.Users;

namespace backend.Dtos.Auth;

public record AuthResponseDto(
    [property: JsonPropertyName("accessToken")] string Token,
    [property: JsonPropertyName("refreshToken")] string? RefreshToken,
    UserDto User
);

