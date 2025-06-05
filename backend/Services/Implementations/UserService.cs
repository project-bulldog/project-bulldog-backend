using backend.Data;
using backend.Dtos.Auth;
using backend.Dtos.Users;
using backend.Helpers;
using backend.Mappers;
using backend.Models;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly BulldogDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(BulldogDbContext context, ILogger<UserService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<UserDto>> GetUsersAsync()
        {
            _logger.LogInformation("Fetching all users");

            var users = await _context.Users
                .Include(u => u.Summaries)
                .ThenInclude(s => s.ActionItems)
                .ToListAsync();

            return [.. users.Select(UserMapper.ToDto)];
        }


        public async Task<UserDto?> GetUserAsync(Guid id)
        {
            _logger.LogInformation("Fetching user with id {Id}", id);

            var user = await _context.Users
                .Include(u => u.Summaries)
                .ThenInclude(s => s.ActionItems)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                _logger.LogWarning("User with id {Id} not found", id);
                return null;
            }

            return UserMapper.ToDto(user);
        }

        public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                DisplayName = dto.DisplayName
            };

            var userId = user.Id;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User created with id {Id}", userId);

            return UserMapper.ToDto(user);
        }

        public async Task<User> RegisterUserAsync(CreateUserDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                _logger.LogWarning("Registration failed: Email {Email} already registered", LogSanitizer.SanitizeForLog(dto.Email));
                throw new InvalidOperationException("Email already registered.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                DisplayName = dto.DisplayName
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User registered successfully with id {Id}", user.Id);
            return user;
        }

        public async Task<User> ValidateUserAsync(LoginRequestDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login failed: User with email {Email} not found", LogSanitizer.SanitizeForLog(request.Email));
                throw new InvalidOperationException("Invalid credentials");
            }

            _logger.LogInformation("User {Id} authenticated successfully", user.Id);
            return user;
        }

        public async Task<bool> UpdateUserAsync(Guid id, UpdateUserDto updateDto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Update failed: user with id {Id} not found", id);
                return false;
            }

            if (updateDto.Email is not null)
            {
                user.Email = updateDto.Email;
            }

            if (updateDto.DisplayName is not null)
            {
                user.DisplayName = updateDto.DisplayName;
            }

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("User with id {Id} updated successfully", id);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(u => u.Id == id))
                {
                    _logger.LogWarning("Update failed: user with id {Id} not found", id);
                    return false;
                }

                _logger.LogError("Concurrency error occurred while updating user with id {Id}", id);
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            _logger.LogInformation("Deleting user with id {Id}", id);
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Delete failed: user with id {Id} not found", id);
                return false;
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User with id {Id} deleted successfully", id);
            return true;
        }
    }
}
