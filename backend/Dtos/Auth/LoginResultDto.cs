namespace backend.Dtos.Auth;

public class LoginResultDto
{
    public AuthResponseDto? Auth { get; set; }
    public TwoFactorPendingDto? TwoFactor { get; set; }

    public static LoginResultDto FromAuth(AuthResponseDto dto) => new() { Auth = dto };
    public static LoginResultDto FromTwoFactor(TwoFactorPendingDto dto) => new() { TwoFactor = dto };

    public bool IsTwoFactorRequired => TwoFactor != null;
    public bool IsAuthenticated => Auth != null;
}
