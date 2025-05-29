using System.Security.Claims;
using backend.Models;

namespace backend.Services.Auth;

public interface IJwtService
{
    string GenerateToken(User user);
    ClaimsPrincipal? ValidateToken(string token);
}
