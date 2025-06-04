namespace backend.Services.Auth.Implementations;

using backend.Extensions;
using backend.Services.Auth.Interfaces;

public class CurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserProvider(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Guid UserId
    {
        get
        {
            var user = (_http.HttpContext?.User) ?? throw new UnauthorizedAccessException("No authenticated user found.");
            return user.GetUserId();
        }
    }
}
