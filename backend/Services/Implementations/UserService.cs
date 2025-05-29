using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.Auth;
using backend.Dtos.Summaries;
using backend.Dtos.Users;
using backend.Models;
using backend.Services.Auth;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly BulldogDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IJwtService _jwt;

        public UserService(BulldogDbContext context, ILogger<UserService> logger, IJwtService jwt)
        {
            _context = context;
            _logger = logger;
            _jwt = jwt;
        }

        public async Task<IEnumerable<UserDto>> GetUsersAsync()
        {
            _logger.LogInformation("Fetching all users");
            var users = await _context.Users
                .Include(u => u.Summaries)
                .ThenInclude(s => s.ActionItems)
                .ToListAsync();

            return [.. users.Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Summaries = [.. u.Summaries.Select(s => new SummaryDto
                {
                    Id = s.Id,
                    ActionItems = [.. s.ActionItems.Select(ai => new ActionItemDto
                    {
                        Id = ai.Id,
                        Text = ai.Text,
                        IsDone = ai.IsDone,
                        DueAt = ai.DueAt
                    })]
                })]
            })];
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

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Summaries = [.. user.Summaries.Select(s => new SummaryDto
                {
                    Id = s.Id,
                    ActionItems = [.. s.ActionItems.Select(ai => new ActionItemDto
                    {
                        Id = ai.Id,
                        Text = ai.Text,
                        IsDone = ai.IsDone,
                        DueAt = ai.DueAt
                    })]
                })]
            };
        }

        public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                DisplayName = dto.DisplayName
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User created with id {Id}", user.Id);

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Summaries = [] //new users won't have summaries yet
            };
        }

        public async Task<AuthResponse> RegisterUserAsync(CreateUserDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                _logger.LogWarning("Registration failed: Email {Email} already registered", dto.Email);
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

            var token = _jwt.GenerateToken(user);
            return new AuthResponse(token, new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName
            });
        }

        public async Task<AuthResponse> LoginUserAsync(LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login failed: User with email {Email} not found", request.Email);
                throw new InvalidOperationException("Invalid credentials");
            }

            var token = _jwt.GenerateToken(user);
            _logger.LogInformation("User {Id} logged in successfully", user.Id);

            return new AuthResponse(token, new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName
            });
        }

        public async Task<bool> UpdateUserAsync(Guid id, UpdateUserDto updateDto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("Update failed: user with id {Id} not found", id);
                return false;
            }

            user.Email = updateDto.Email;
            user.DisplayName = updateDto.DisplayName;

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
