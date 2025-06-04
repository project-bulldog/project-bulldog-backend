namespace backend.Services.Auth.Interfaces;

public interface ICurrentUserProvider
{
    Guid UserId { get; }
}
