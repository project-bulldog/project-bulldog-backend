using backend.Dtos.Auth;
using backend.Dtos.Users;
using backend.Models;

namespace backend.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetUsersAsync();
    Task<UserDto?> GetUserAsync(Guid id);
    Task<User?> GetUserEntityAsync(Guid id);
    Task<UserDto> CreateUserAsync(CreateUserDto createDto);
    Task<bool> UpdateUserAsync(Guid id, UpdateUserDto updateDto);
    Task<bool> DeleteUserAsync(Guid id);
    Task<User> RegisterUserAsync(CreateUserDto dto);
    Task<User> ValidateUserAsync(LoginRequestDto request);
}
