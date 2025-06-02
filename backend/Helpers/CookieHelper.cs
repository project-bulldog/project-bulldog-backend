namespace backend.Helpers;

public static class CookieHelper
{
    public static void ClearRefreshToken(HttpResponse response)
    {
        response.Cookies.Append("refreshToken", "", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(-1)
        });
    }
}
