using backend.Models.Auth;

namespace backend.Services.Auth.Interfaces;

public interface ICookieService
{
    void SetRefreshToken(HttpResponse response, RefreshToken token);
    void ClearRefreshToken(HttpResponse response);
}
