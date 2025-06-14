using System.Security.Claims;

namespace backend.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var rawId = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(rawId, out var userId))
            throw new UnauthorizedAccessException("Invalid or missing user ID.");

        return userId;
    }
}
