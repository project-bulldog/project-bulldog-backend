namespace backend.Models.Auth;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string EncryptedToken { get; set; } = string.Empty;

    public string HashedToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public User User { get; set; } = null!;
}
