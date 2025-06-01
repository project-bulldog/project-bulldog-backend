using backend.Data;
using backend.Models.Auth;
using backend.Services.Auth.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;

public class TokenCleanupServiceTests : IDisposable
{
    private readonly BulldogDbContext _dbContext;
    private readonly Mock<ILogger<TokenCleanupService>> _mockLogger;
    private readonly TokenCleanupService _service;

    public TokenCleanupServiceTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new BulldogDbContext(options);
        _mockLogger = new Mock<ILogger<TokenCleanupService>>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("TokenCleanup:RevokedRetentionDays", "7")
            ])
            .Build();

        _service = new TokenCleanupService(_dbContext, _mockLogger.Object, config);
    }

    [Fact]
    public async Task CleanupExpiredTokensAsync_WhenRevokedBeyondRetention_RemovesToken()
    {
        var oldRevoked = new RefreshToken
        {
            Id = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow.AddDays(-8)
        };

        var recentRevoked = new RefreshToken
        {
            Id = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow.AddDays(-3)
        };

        var nullRevoked = new RefreshToken
        {
            Id = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
            RevokedAt = null
        };

        await _dbContext.RefreshTokens.AddRangeAsync(oldRevoked, recentRevoked, nullRevoked);
        await _dbContext.SaveChangesAsync();

        await _service.CleanupExpiredTokensAsync();

        var tokens = await _dbContext.RefreshTokens.ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.DoesNotContain(tokens, t => t.Id == oldRevoked.Id);
        Assert.Contains(tokens, t => t.Id == recentRevoked.Id);
        Assert.Contains(tokens, t => t.Id == nullRevoked.Id);
    }

    [Fact]
    public async Task CleanupExpiredTokensAsync_WhenExpiredTokensExist_RemovesTokens()
    {
        // Arrange
        var expiredTokens = new List<RefreshToken>
        {
            new() { Id = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddHours(-1) },
            new() { Id = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddHours(-2) }
        };

        var validTokens = new List<RefreshToken>
        {
            new() { Id = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddHours(1) },
            new() { Id = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddHours(2) }
        };

        await _dbContext.RefreshTokens.AddRangeAsync(expiredTokens);
        await _dbContext.RefreshTokens.AddRangeAsync(validTokens);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.CleanupExpiredTokensAsync();

        // Assert
        var remainingTokens = await _dbContext.RefreshTokens.ToListAsync();
        Assert.Equal(2, remainingTokens.Count);
        Assert.All(remainingTokens, token => Assert.True(token.ExpiresAt > DateTime.UtcNow));

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                o != null && o.ToString()!.Contains("Cleaned up 2 expired/revoked refresh tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

    }

    [Fact]
    public async Task CleanupExpiredTokensAsync_WhenNoExpiredTokens_DoesNothing()
    {
        // Arrange
        var validTokens = new List<RefreshToken>
        {
            new() { Id = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddHours(1) },
            new() { Id = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddHours(2) }
        };

        await _dbContext.RefreshTokens.AddRangeAsync(validTokens);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.CleanupExpiredTokensAsync();

        // Assert
        var remainingTokens = await _dbContext.RefreshTokens.ToListAsync();
        Assert.Equal(2, remainingTokens.Count);
        Assert.All(remainingTokens, token => Assert.True(token.ExpiresAt > DateTime.UtcNow));

        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) =>
                    o != null && o.ToString()!.Contains("Cleaned up")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
