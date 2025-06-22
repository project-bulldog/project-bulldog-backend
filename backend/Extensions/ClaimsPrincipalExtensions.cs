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

    public static string GetUserEmail(this ClaimsPrincipal user)
    {
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");

        if (string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedAccessException("Missing email in token.");

        return email;
    }
}
