using backend.Dtos.Auth;
using backend.Models;

namespace backend.Services.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(User user, HttpResponse httpResponse);
    Task LogoutAllSessionsAsync(Guid userId, HttpResponse response);

}
