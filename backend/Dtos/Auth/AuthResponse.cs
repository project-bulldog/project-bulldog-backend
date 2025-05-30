using backend.Dtos.Users;

namespace backend.Dtos.Auth;

public record AuthResponse(string Token, UserDto User);
