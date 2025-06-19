namespace backend.Dtos.Auth;

public class RefreshResultDto
{
    public string AccessToken { get; set; } = null!;
    public string? RefreshToken { get; set; }

    public static RefreshResultDto ForNormalBrowser(string accessToken) => new()
    {
        AccessToken = accessToken,
        RefreshToken = null
    };

    public static RefreshResultDto ForIos(string accessToken, string refreshToken) => new()
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken
    };
}
