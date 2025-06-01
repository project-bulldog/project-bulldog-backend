using System.Text.Json.Serialization;
using backend.Dtos.Users;

namespace backend.Dtos.Auth;

public record AuthResponse(
    [property: JsonPropertyName("accessToken")] string Token,
    UserDto User
);
