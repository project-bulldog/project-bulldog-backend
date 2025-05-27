using backend.Dtos;
using backend.Models;

namespace backend.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetUsersAsync();
    Task<UserDto?> GetUserAsync(Guid id);
    Task<UserDto> CreateUserAsync(User user);
    Task<bool> UpdateUserAsync(Guid id, User user);
    Task<bool> DeleteUserAsync(Guid id);
}
