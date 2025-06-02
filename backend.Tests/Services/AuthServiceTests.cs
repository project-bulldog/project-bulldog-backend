using System.Net;
using backend.Data;
using backend.Dtos.Auth;
using backend.Models;
using backend.Models.Auth;
using backend.Services.Auth;
using backend.Services.Auth.Implementations;
using backend.Services.Auth.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;
public class AuthServiceTests
{
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly BulldogDbContext _context;
    private readonly Mock<ICookieService> _cookieServiceMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly DefaultHttpContext _httpContext;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _jwtServiceMock = new Mock<IJwtService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _cookieServiceMock = new Mock<ICookieService>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BulldogDbContext(options);

        _httpContext = new DefaultHttpContext();
        _httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        _httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X)"; // Set iOS user agent

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext).Returns(_httpContext);

        _authService = new AuthService(
            _jwtServiceMock.Object,
            _tokenServiceMock.Object,
            _cookieServiceMock.Object,
            _context,
            _loggerMock.Object,
            httpContextAccessor.Object
        );
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnAuthResponseWithTokens()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var response = _httpContext.Response;

        var expectedAccessToken = "test.access.token";
        var expectedRefreshToken = ("encrypted.token", "hashed.token", "plain.token");

        _jwtServiceMock.Setup(x => x.GenerateToken(user)).Returns(expectedAccessToken);
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(expectedRefreshToken);
        _cookieServiceMock.Setup(x => x.SetRefreshToken(It.IsAny<HttpResponse>(), It.IsAny<RefreshToken>()));

        // Act
        var result = await _authService.LoginAsync(user, response);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAccessToken, result.AccessToken);
        Assert.Equal(expectedRefreshToken.Item1, result.RefreshToken); // Should return refresh token for iOS
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal(user.Email, result.User.Email);
        Assert.Equal(user.DisplayName, result.User.DisplayName);

        var savedToken = await _context.RefreshTokens.FirstOrDefaultAsync();
        Assert.NotNull(savedToken);
        Assert.Equal(user.Id, savedToken.UserId);
        Assert.Equal(expectedRefreshToken.Item2, savedToken.HashedToken);

        _cookieServiceMock.Verify(x => x.SetRefreshToken(It.IsAny<HttpResponse>(), It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAllSessionsAsync_ShouldRevokeTokensAndClearCookie()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var response = _httpContext.Response;

        _cookieServiceMock.Setup(x => x.ClearRefreshToken(It.IsAny<HttpResponse>()));

        // Act
        await _authService.LogoutAllSessionsAsync(userId, response);

        // Assert
        _tokenServiceMock.Verify(x => x.RevokeAllUserTokensAsync(userId, "Manual logout"), Times.Once);
        _cookieServiceMock.Verify(x => x.ClearRefreshToken(It.IsAny<HttpResponse>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnRefreshTokenForIOS()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var response = _httpContext.Response;
        _httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X)";

        var expectedAccessToken = "test.access.token";
        var expectedRefreshToken = ("encrypted.token", "hashed.token", "plain.token");

        _jwtServiceMock.Setup(x => x.GenerateToken(user)).Returns(expectedAccessToken);
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(expectedRefreshToken);
        _cookieServiceMock.Setup(x => x.SetRefreshToken(It.IsAny<HttpResponse>(), It.IsAny<RefreshToken>()));

        // Act
        var result = await _authService.LoginAsync(user, response);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAccessToken, result.AccessToken);
        Assert.Equal(expectedRefreshToken.Item1, result.RefreshToken); // Should return refresh token for iOS
        _cookieServiceMock.Verify(x => x.SetRefreshToken(It.IsAny<HttpResponse>(), It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ShouldRevokeTokenAndReturnSessionInfo()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var encryptedToken = "test.encrypted.token";
        var response = _httpContext.Response;
        var expectedSessionInfo = new SessionMetadataDto
        {
            TokenId = Guid.NewGuid(),
            Ip = "127.0.0.1",
            UserAgent = "TestAgent"
        };

        _tokenServiceMock.Setup(x => x.RevokeTokenAsync(encryptedToken, userId))
            .ReturnsAsync(expectedSessionInfo);
        _cookieServiceMock.Setup(x => x.ClearRefreshToken(It.IsAny<HttpResponse>()));

        // Act
        var result = await _authService.LogoutAsync(userId, encryptedToken, response);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSessionInfo.Ip, result.Ip);
        Assert.Equal(expectedSessionInfo.UserAgent, result.UserAgent);
        Assert.Equal(expectedSessionInfo.TokenId, result.TokenId);

        _cookieServiceMock.Verify(x => x.ClearRefreshToken(It.IsAny<HttpResponse>()), Times.Once);
    }
}
