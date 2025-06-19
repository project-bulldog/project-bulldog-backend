using System.Net;
using backend.Data;
using backend.Dtos.Auth;
using backend.Models;
using backend.Models.Auth;
using backend.Services.Auth;
using backend.Services.Auth.Implementations;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
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
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<ITwoFactorService> _twoFactorServiceMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly DefaultHttpContext _httpContext;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _jwtServiceMock = new Mock<IJwtService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _cookieServiceMock = new Mock<ICookieService>();
        _userServiceMock = new Mock<IUserService>();
        _twoFactorServiceMock = new Mock<ITwoFactorService>();
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
            httpContextAccessor.Object,
            _userServiceMock.Object,
            _twoFactorServiceMock.Object
        );
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnAuthResponseWithTokens_WhenTwoFactorDisabled()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            TwoFactorEnabled = false
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
        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsTwoFactorRequired);
        Assert.NotNull(result.Auth);
        Assert.Equal(expectedAccessToken, result.Auth.AccessToken);
        Assert.Equal(expectedRefreshToken.Item1, result.Auth.RefreshToken); // Should return refresh token for iOS
        Assert.Equal(user.Id, result.Auth.User.Id);
        Assert.Equal(user.Email, result.Auth.User.Email);
        Assert.Equal(user.DisplayName, result.Auth.User.DisplayName);

        var savedToken = await _context.RefreshTokens.FirstOrDefaultAsync();
        Assert.NotNull(savedToken);
        Assert.Equal(user.Id, savedToken.UserId);
        Assert.Equal(expectedRefreshToken.Item2, savedToken.HashedToken);

        _cookieServiceMock.Verify(x => x.SetRefreshToken(It.IsAny<HttpResponse>(), It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnTwoFactorPending_WhenTwoFactorEnabled()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            TwoFactorEnabled = true
        };

        var response = _httpContext.Response;

        _twoFactorServiceMock.Setup(x => x.GenerateAndSendOtpAsync(user)).ReturnsAsync("123456");

        // Act
        var result = await _authService.LoginAsync(user, response);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsAuthenticated);
        Assert.True(result.IsTwoFactorRequired);
        Assert.NotNull(result.TwoFactor);
        Assert.Equal(user.Id, result.TwoFactor.UserId);

        _twoFactorServiceMock.Verify(x => x.GenerateAndSendOtpAsync(user), Times.Once);
    }

    [Fact]
    public async Task AuthenticateUserAsync_ShouldReturnUser_WhenValidCredentials()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
        };

        var loginRequest = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "password123"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.AuthenticateUserAsync(loginRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.DisplayName, result.DisplayName);
    }

    [Fact]
    public async Task AuthenticateUserAsync_ShouldThrowUnauthorizedAccessException_WhenUserNotFound()
    {
        // Arrange
        var loginRequest = new LoginRequestDto
        {
            Email = "nonexistent@example.com",
            Password = "password123"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _authService.AuthenticateUserAsync(loginRequest));
    }

    [Fact]
    public async Task AuthenticateUserAsync_ShouldThrowUnauthorizedAccessException_WhenInvalidPassword()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword")
        };

        var loginRequest = new LoginRequestDto
        {
            Email = "test@example.com",
            Password = "wrongpassword"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _authService.AuthenticateUserAsync(loginRequest));
    }

    [Fact]
    public async Task VerifyTwoFactorAsync_ShouldReturnAuthResponse_WhenValidCode()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            TwoFactorEnabled = true
        };

        var response = _httpContext.Response;
        var code = "123456";
        var userId = user.Id;

        var expectedAccessToken = "test.access.token";
        var expectedRefreshToken = ("encrypted.token", "hashed.token", "plain.token");

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _twoFactorServiceMock.Setup(x => x.VerifyOtpAsync(user, code)).ReturnsAsync(true);
        _jwtServiceMock.Setup(x => x.GenerateToken(user)).Returns(expectedAccessToken);
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(expectedRefreshToken);
        _cookieServiceMock.Setup(x => x.SetRefreshToken(It.IsAny<HttpResponse>(), It.IsAny<RefreshToken>()));

        // Act
        var result = await _authService.VerifyTwoFactorAsync(userId, code, response);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsAuthenticated);
        Assert.False(result.IsTwoFactorRequired);
        Assert.NotNull(result.Auth);
        Assert.Equal(expectedAccessToken, result.Auth.AccessToken);
        Assert.Equal(expectedRefreshToken.Item1, result.Auth.RefreshToken);
        Assert.Equal(user.Id, result.Auth.User.Id);

        _twoFactorServiceMock.Verify(x => x.VerifyOtpAsync(user, code), Times.Once);
    }

    [Fact]
    public async Task VerifyTwoFactorAsync_ShouldThrowUnauthorizedAccessException_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var code = "123456";
        var response = _httpContext.Response;

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _authService.VerifyTwoFactorAsync(userId, code, response));
    }

    [Fact]
    public async Task VerifyTwoFactorAsync_ShouldThrowInvalidOperationException_WhenTwoFactorNotEnabled()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            TwoFactorEnabled = false
        };

        var response = _httpContext.Response;
        var code = "123456";
        var userId = user.Id;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _authService.VerifyTwoFactorAsync(userId, code, response));
    }

    [Fact]
    public async Task VerifyTwoFactorAsync_ShouldThrowUnauthorizedAccessException_WhenInvalidCode()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            TwoFactorEnabled = true
        };

        var response = _httpContext.Response;
        var code = "123456";
        var userId = user.Id;

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _twoFactorServiceMock.Setup(x => x.VerifyOtpAsync(user, code)).ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _authService.VerifyTwoFactorAsync(userId, code, response));
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
            DisplayName = "Test User",
            TwoFactorEnabled = false
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
        Assert.True(result.IsAuthenticated);
        Assert.Equal(expectedAccessToken, result.Auth.AccessToken);
        Assert.Equal(expectedRefreshToken.Item1, result.Auth.RefreshToken); // Should return refresh token for iOS
        _cookieServiceMock.Verify(x => x.SetRefreshToken(It.IsAny<HttpResponse>(), It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldNotReturnRefreshTokenForNonIOS()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            TwoFactorEnabled = false
        };

        var response = _httpContext.Response;
        _httpContext.Request.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

        var expectedAccessToken = "test.access.token";
        var expectedRefreshToken = ("encrypted.token", "hashed.token", "plain.token");

        _jwtServiceMock.Setup(x => x.GenerateToken(user)).Returns(expectedAccessToken);
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns(expectedRefreshToken);
        _cookieServiceMock.Setup(x => x.SetRefreshToken(It.IsAny<HttpResponse>(), It.IsAny<RefreshToken>()));

        // Act
        var result = await _authService.LoginAsync(user, response);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsAuthenticated);
        Assert.Equal(expectedAccessToken, result.Auth.AccessToken);
        Assert.Null(result.Auth.RefreshToken); // Should not return refresh token for non-iOS
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

    [Fact]
    public async Task LogoutAsync_ShouldReturnNull_WhenTokenNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var encryptedToken = "test.encrypted.token";
        var response = _httpContext.Response;

        _tokenServiceMock.Setup(x => x.RevokeTokenAsync(encryptedToken, userId))
            .ReturnsAsync((SessionMetadataDto?)null);
        _cookieServiceMock.Setup(x => x.ClearRefreshToken(It.IsAny<HttpResponse>()));

        // Act
        var result = await _authService.LogoutAsync(userId, encryptedToken, response);

        // Assert
        Assert.Null(result);
        _cookieServiceMock.Verify(x => x.ClearRefreshToken(It.IsAny<HttpResponse>()), Times.Once);
    }
}
