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

        public async Task<User?> GetUserEntityAsync(Guid id)
        {
            return await _context.Users.FindAsync(id);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == NormalizeEmail(email));
        }

        public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
        {
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            var utcNow = DateTime.UtcNow;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                PasswordHash = hashedPassword,
                TwoFactorEnabled = dto.EnableTwoFactor,
                CreatedAtUtc = utcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User created with id {Id}", user.Id);

            return UserMapper.ToDto(user);
        }

        public async Task<User> RegisterUserAsync(CreateUserDto dto)
        {
            var normalizedEmail = NormalizeEmail(dto.Email);
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            {
                _logger.LogWarning("Registration failed: Email {Email} already registered", LogSanitizer.SanitizeForLog(dto.Email));
                throw new InvalidOperationException("Email already registered.");
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            var utcNow = DateTime.UtcNow;

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                DisplayName = dto.DisplayName,
                PasswordHash = hashedPassword,
                TwoFactorEnabled = dto.EnableTwoFactor,
                CreatedAtUtc = utcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User registered successfully with id {Id}", user.Id);
            return user;
        }

        public async Task<User> ValidateUserAsync(LoginRequestDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == NormalizeEmail(request.Email));
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

            if (!string.IsNullOrWhiteSpace(updateDto.Email))
            {
                user.Email = updateDto.Email;
            }

            if (!string.IsNullOrWhiteSpace(updateDto.DisplayName))
            {
                user.DisplayName = updateDto.DisplayName;
            }

            if (updateDto.EmailVerified.HasValue)
            {
                user.EmailVerified = updateDto.EmailVerified.Value;
            }

            if (updateDto.PhoneNumberVerified.HasValue)
            {
                user.PhoneNumberVerified = updateDto.PhoneNumberVerified.Value;
            }

            if (!string.IsNullOrWhiteSpace(updateDto.TimeZoneId))
            {
                user.TimeZoneId = TimeZoneHelpers.NormalizeTimeZoneId(updateDto.TimeZoneId);
                _logger.LogInformation("Timezone updated for user {Id} to {Timezone}", id, LogSanitizer.SanitizeForLog(user.TimeZoneId));
            }

            if (!string.IsNullOrWhiteSpace(updateDto.NewPassword))
            {
                UpdatePassword(user, updateDto.CurrentPassword, updateDto.NewPassword, id);
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

        #region Private Methods
        private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        private void UpdatePassword(User user, string current, string updated, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(current) || !BCrypt.Net.BCrypt.Verify(current, user.PasswordHash))
            {
                _logger.LogWarning("Password update failed: invalid current password for user {Id}", userId);
                throw new UnauthorizedAccessException("Current password is incorrect.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updated);
            _logger.LogInformation("Password updated for user {Id}", userId);
        }
        #endregion
    }
}
