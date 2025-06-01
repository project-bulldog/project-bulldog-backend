using System.Security.Cryptography;
using System.Text;
using backend.Data;
using backend.Models.Auth;
using backend.Services.Auth.Implementations;
using backend.Services.Auth.Interfaces;
using backend.Tests.TestHelpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;

public class TokenServiceTests
{
    private readonly FakeProtector _fakeProtector;
    private readonly Mock<ILogger<TokenService>> _mockLogger;
    private readonly BulldogDbContext _context;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly TokenService _tokenService;

    public TokenServiceTests()
    {
        _fakeProtector = new FakeProtector();
        _mockLogger = new Mock<ILogger<TokenService>>();
        _mockJwtService = new Mock<IJwtService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BulldogDbContext(options);

        _tokenService = new TokenService(
            _fakeProtector,
            _mockLogger.Object,
            _context,
            _mockJwtService.Object,
            _mockHttpContextAccessor.Object
        );
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnValidTokens()
    {
        var (encryptedToken, hashedToken, rawToken) = _tokenService.GenerateRefreshToken();

        Assert.NotNull(encryptedToken);
        Assert.NotNull(hashedToken);
        Assert.NotNull(rawToken);
        Assert.NotEqual(encryptedToken, hashedToken);
        Assert.NotEqual(encryptedToken, rawToken);
        Assert.NotEqual(hashedToken, rawToken);
    }

    [Fact]
    public void ComputeSha256_ShouldReturnValidHash()
    {
        var input = "test-token";
        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

        var result = _tokenService.ComputeSha256(input);

        Assert.Equal(expectedHash, result);
    }

    [Fact]
    public void DecryptToken_ValidToken_ShouldReturnDecryptedToken()
    {
        var encryptedToken = "encrypted-token";
        var expectedDecrypted = "decrypted-token";
        _fakeProtector.ToReturnOnUnprotect = "decrypted-token";

        var result = _tokenService.DecryptToken(encryptedToken);

        Assert.Equal(expectedDecrypted, result);
    }

    [Fact]
    public void DecryptToken_InvalidToken_ShouldThrowException()
    {
        // Arrange
        var tamperedToken = "invalid-token";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            _tokenService.DecryptToken(tamperedToken));
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_ShouldRevokeAllActiveTokens()
    {
        var userId = Guid.NewGuid();
        var tokens = new List<RefreshToken>
        {
            new() { UserId = userId, IsRevoked = false },
            new() { UserId = userId, IsRevoked = false }
        };

        _context.RefreshTokens.AddRange(tokens);
        await _context.SaveChangesAsync();

        await _tokenService.RevokeAllUserTokensAsync(userId, "Test reason");

        var updated = await _context.RefreshTokens.ToListAsync();
        Assert.All(updated, token => Assert.True(token.IsRevoked));
        Assert.All(updated, token => Assert.NotNull(token.RevokedAt));
        Assert.All(updated, token => Assert.Equal("Test reason", token.RevokedReason));
    }
}
