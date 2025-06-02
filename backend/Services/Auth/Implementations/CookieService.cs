using backend.Models.Auth;
using backend.Services.Auth.Interfaces;

namespace backend.Services.Auth.Implementations;

public class CookieService : ICookieService
{
    private readonly bool _isDevelopment;

    public CookieService(IWebHostEnvironment env)
    {
        _isDevelopment = env.IsDevelopment();
    }

    public void ClearRefreshToken(HttpResponse response)
    {
        response.Cookies.Append("refreshToken", "", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = DateTime.UtcNow.AddDays(-1)
        });
    }

    public void SetRefreshToken(HttpResponse response, RefreshToken token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = token.ExpiresAt
        };

        response.Cookies.Append("refreshToken", token.EncryptedToken, cookieOptions);
    }
}
