namespace backend.Services.Auth.Implementations;

using backend.Data;
using backend.Services.Auth.Interfaces;
using Microsoft.EntityFrameworkCore;

public class TokenCleanupService : ITokenCleanupService
{
    private readonly BulldogDbContext _db;
    private readonly ILogger<TokenCleanupService> _logger;
    private readonly int _retentionDays;

    public TokenCleanupService(BulldogDbContext db, ILogger<TokenCleanupService> logger, IConfiguration config)
    {
        _db = db;
        _logger = logger;
        _retentionDays = int.Parse(config["TokenCleanup:RevokedRetentionDays"] ?? "7");
    }

    public async Task CleanupExpiredTokensAsync()
    {
        var now = DateTime.UtcNow;
        var revokeCutoff = now.AddDays(-_retentionDays);

        var tokensToDelete = await _db.RefreshTokens
            .Where(t =>
                t.ExpiresAt < now ||
                (t.IsRevoked && t.RevokedAt != null && t.RevokedAt < revokeCutoff))
            .ToListAsync();

        if (tokensToDelete.Any())
        {
            _db.RefreshTokens.RemoveRange(tokensToDelete);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} expired/revoked refresh tokens.", tokensToDelete.Count);
        }
    }
}
