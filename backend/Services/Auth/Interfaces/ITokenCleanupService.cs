namespace backend.Services.Auth.Interfaces;

public interface ITokenCleanupService
{
    Task CleanupExpiredTokensAsync();
}
