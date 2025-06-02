using System.Text;
using backend.Data;
using backend.Models;
using backend.Models.Auth;
using backend.Services.Auth;
using backend.Services.Auth.Implementations;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using backend.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace backend.Tests.Services;

public class TokenServiceTests : IDisposable
{
    private readonly FakeProtector _fakeProtector;
    private readonly Mock<ILogger<TokenService>> _mockLogger;
    private readonly BulldogDbContext _context;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly TokenService _tokenService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ICookieService> _mockCookieService;

    public TokenServiceTests()
    {
        _fakeProtector = new FakeProtector();
        _mockLogger = new Mock<ILogger<TokenService>>();
        _mockJwtService = new Mock<IJwtService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockCookieService = new Mock<ICookieService>();
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BulldogDbContext(options);

        _tokenService = new TokenService(
            _fakeProtector,
            _mockLogger.Object,
            _context,
            _mockJwtService.Object,
            _mockNotificationService.Object,
            _mockCookieService.Object
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
    public void DecryptToken_ValidToken_ShouldReturnDecryptedToken()
    {
        // Arrange
        var rawToken = "decrypted-token";
        var encryptedBytes = Encoding.UTF8.GetBytes(rawToken);
        var encryptedToken = Convert.ToBase64String(encryptedBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        _fakeProtector.ToReturnOnUnprotect = rawToken;

        // Act
        var result = _tokenService.DecryptToken(encryptedToken);

        // Assert
        Assert.Equal(rawToken, result);
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

    [Fact]
    public async Task ValidateAndRotateRefreshTokenAsync_WithValidToken_ReturnsNewTokens()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        var (encryptedToken, hashedToken, _) = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            EncryptedToken = encryptedToken,
            HashedToken = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false
        };

        await _context.Users.AddAsync(user);
        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();

        var response = new Mock<HttpResponse>();

        _mockJwtService.Setup(x => x.GenerateToken(It.IsAny<User>()))
            .Returns("new-access-token");

        // Act
        var (newAccessToken, newEncryptedToken) = await _tokenService.ValidateAndRotateRefreshTokenAsync(
            encryptedToken,
            response.Object,
            "127.0.0.1",
            "TestBrowser"
        );

        // Assert
        Assert.Equal("new-access-token", newAccessToken);
        Assert.NotNull(newEncryptedToken);
        Assert.NotEqual(encryptedToken, newEncryptedToken);

        var oldToken = await _context.RefreshTokens.FindAsync(refreshToken.Id);
        Assert.NotNull(oldToken);
        Assert.True(oldToken!.IsRevoked);
        Assert.Equal("Token rotated", oldToken.RevokedReason);

        var newToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.EncryptedToken == newEncryptedToken);
        Assert.NotNull(newToken);
        Assert.Equal(user.Id, newToken.UserId);
        Assert.False(newToken.IsRevoked);
        Assert.Equal("127.0.0.1", newToken.CreatedByIp);
        Assert.Equal("TestBrowser", newToken.UserAgent);

        _mockCookieService.Verify(c => c.SetRefreshToken(
            response.Object,
            It.Is<RefreshToken>(t => t.EncryptedToken == newEncryptedToken)
        ), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRotateRefreshTokenAsync_WithInvalidToken_SendsSecurityAlert()
    {
        // Arrange
        var response = new Mock<HttpResponse>();
        _fakeProtector.ThrowOnUnprotect = true;

        // Act
        await Assert.ThrowsAsync<SecurityTokenException>(() =>
            _tokenService.ValidateAndRotateRefreshTokenAsync("invalid-token", response.Object));

        // Assert
        _mockNotificationService.Verify(x => x.SendSecurityAlertAsync(
            It.Is<string>(s => s.Contains("Security Alert: Token Decryption Failed")),
            It.Is<string>(msg => msg.Contains("Failed to decrypt refresh token") && msg.Contains("Possible tampering detected"))
        ), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRotateRefreshTokenAsync_WithRevokedToken_RevokesAllUserTokens()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        var (encryptedToken, hashedToken, _) = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            EncryptedToken = encryptedToken,
            HashedToken = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow.AddHours(-1),
            RevokedReason = "Previous revocation"
        };

        // Add another active token for the same user
        var (encryptedToken2, hashedToken2, _) = _tokenService.GenerateRefreshToken();
        var refreshToken2 = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            EncryptedToken = encryptedToken2,
            HashedToken = hashedToken2,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false
        };

        await _context.Users.AddAsync(user);
        await _context.RefreshTokens.AddRangeAsync(refreshToken, refreshToken2);
        await _context.SaveChangesAsync();

        var response = new Mock<HttpResponse>();

        // Act
        await Assert.ThrowsAsync<SecurityTokenException>(() =>
            _tokenService.ValidateAndRotateRefreshTokenAsync(encryptedToken, response.Object));

        // Assert
        var allTokens = await _context.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        Assert.All(allTokens, token => Assert.True(token.IsRevoked));
        Assert.All(allTokens, token =>
        {
            if (token.Id == refreshToken.Id)
            {
                Assert.Equal("Previous revocation", token.RevokedReason);
            }
            else
            {
                Assert.Equal("Refresh token reuse detected", token.RevokedReason);
            }
        });
    }

    [Fact]
    public async Task ValidateAndRotateRefreshTokenAsync_WithRevokedToken_SendsSecurityAlert()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        var (encryptedToken, hashedToken, _) = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            EncryptedToken = encryptedToken,
            HashedToken = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow.AddHours(-1)
        };

        await _context.Users.AddAsync(user);
        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();

        var response = new Mock<HttpResponse>();

        // Act
        await Assert.ThrowsAsync<SecurityTokenException>(() =>
            _tokenService.ValidateAndRotateRefreshTokenAsync(encryptedToken, response.Object));

        // Assert
        _mockNotificationService.Verify(x => x.SendSecurityAlertAsync(
            "Security Alert: Refresh Token Reuse Detected",
            It.Is<string>(msg =>
                msg.Contains("Refresh token reuse detected") &&
                msg.Contains($"user ID: {user.Id}") &&
                msg.Contains("All sessions revoked"))
        ), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRotateRefreshTokenAsync_WithExpiredToken_ThrowsSecurityTokenException()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        var (encryptedToken, hashedToken, _) = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            EncryptedToken = encryptedToken,
            HashedToken = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            IsRevoked = false
        };

        await _context.Users.AddAsync(user);
        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();

        var response = new Mock<HttpResponse>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<SecurityTokenException>(() =>
            _tokenService.ValidateAndRotateRefreshTokenAsync(encryptedToken, response.Object));

        Assert.Equal("Refresh token has expired.", exception.Message);

        // Verify token was marked as revoked
        var updatedToken = await _context.RefreshTokens.FindAsync(refreshToken.Id);
        Assert.NotNull(updatedToken);
        Assert.True(updatedToken!.IsRevoked);
        Assert.Equal("Expired", updatedToken!.RevokedReason);
    }

    [Fact]
    public async Task ValidateAndRotateRefreshTokenAsync_WithNonExistentToken_SendsSecurityAlert()
    {
        // Arrange
        var (encryptedToken, _, _) = _tokenService.GenerateRefreshToken();
        var response = new Mock<HttpResponse>();

        // Act
        await Assert.ThrowsAsync<SecurityTokenException>(() =>
            _tokenService.ValidateAndRotateRefreshTokenAsync(encryptedToken, response.Object));

        // Assert
        _mockNotificationService.Verify(x => x.SendSecurityAlertAsync(
            "Security Alert: Unknown Refresh Token Used",
            It.Is<string>(msg => msg.Contains("Unknown refresh token attempted") && msg.Contains("Could indicate forgery"))
        ), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRotateRefreshTokenAsync_WithValidToken_DoesNotSendSecurityAlert()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        var (encryptedToken, hashedToken, _) = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            EncryptedToken = encryptedToken,
            HashedToken = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false
        };

        await _context.Users.AddAsync(user);
        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();

        var response = new Mock<HttpResponse>();

        _mockJwtService.Setup(x => x.GenerateToken(It.IsAny<User>()))
            .Returns("new-access-token");

        // Act
        await _tokenService.ValidateAndRotateRefreshTokenAsync(
            encryptedToken,
            response.Object,
            "127.0.0.1",
            "TestBrowser"
        );

        // Assert
        _mockNotificationService.Verify(x => x.SendSecurityAlertAsync(
            It.IsAny<string>(),
            It.IsAny<string>()
        ), Times.Never);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithValidToken_ReturnsSessionInfo()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        var (encryptedToken, hashedToken, _) = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            EncryptedToken = encryptedToken,
            HashedToken = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false,
            CreatedByIp = "127.0.0.1",
            UserAgent = "TestBrowser"
        };

        await _context.Users.AddAsync(user);
        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tokenService.RevokeTokenAsync(encryptedToken, user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(refreshToken.Id, result.TokenId);
        Assert.Equal("127.0.0.1", result.Ip);
        Assert.Equal("TestBrowser", result.UserAgent);

        var updatedToken = await _context.RefreshTokens.FindAsync(refreshToken.Id);
        Assert.NotNull(updatedToken);
        Assert.True(updatedToken!.IsRevoked);
        Assert.Equal("User logout", updatedToken.RevokedReason);
        Assert.NotNull(updatedToken.RevokedAt);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        var (encryptedToken, _, _) = _tokenService.GenerateRefreshToken();
        var userId = Guid.NewGuid();

        // Act
        var result = await _tokenService.RevokeTokenAsync(encryptedToken, userId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndRotateRefreshTokenAsync_WithValidToken_DeletesOldCookieBeforeSettingNew()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", DisplayName = "Test User" };
        var (encryptedToken, hashedToken, _) = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            EncryptedToken = encryptedToken,
            HashedToken = hashedToken,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = false
        };

        await _context.Users.AddAsync(user);
        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();

        var response = new Mock<HttpResponse>();

        _mockJwtService.Setup(x => x.GenerateToken(It.IsAny<User>()))
            .Returns("new-access-token");

        // Act
        await _tokenService.ValidateAndRotateRefreshTokenAsync(
            encryptedToken,
            response.Object,
            "127.0.0.1",
            "TestBrowser"
        );

        // Assert
        _mockCookieService.Verify(c => c.SetRefreshToken(
            response.Object,
            It.Is<RefreshToken>(t => t.EncryptedToken != encryptedToken)
        ), Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
