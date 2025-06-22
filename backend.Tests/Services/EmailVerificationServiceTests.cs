using System.Security.Cryptography;
using System.Text;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using backend.Data;
using backend.Models;
using backend.Services.Auth.Implementations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;

public class EmailVerificationServiceTests
{
    private readonly BulldogDbContext _context;
    private readonly Mock<ILogger<EmailVerificationService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IAmazonSimpleEmailService> _sesServiceMock;
    private readonly Mock<IDataProtectionProvider> _dataProtectionProviderMock;
    private readonly Mock<IDataProtector> _dataProtectorMock;
    private readonly EmailVerificationService _emailVerificationService;

    public EmailVerificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BulldogDbContext(options);

        _loggerMock = new Mock<ILogger<EmailVerificationService>>();
        _configurationMock = new Mock<IConfiguration>();
        _sesServiceMock = new Mock<IAmazonSimpleEmailService>();
        _dataProtectionProviderMock = new Mock<IDataProtectionProvider>();
        _dataProtectorMock = new Mock<IDataProtector>();

        // Setup mock for data protection
        _dataProtectionProviderMock.Setup(p => p.CreateProtector(It.IsAny<string>()))
            .Returns(_dataProtectorMock.Object);

        // Setup mock for configuration
        _configurationMock.Setup(c => c["Frontend:BaseUrl"]).Returns("http://localhost:3000");
        _configurationMock.Setup(c => c["AWS:FromEmail"]).Returns("test@example.com");

        _emailVerificationService = new EmailVerificationService(
            _context,
            _loggerMock.Object,
            _configurationMock.Object,
            _sesServiceMock.Object,
            _dataProtectionProviderMock.Object
        );
    }

    [Fact]
    public async Task GenerateAndSendVerificationEmailAsync_ShouldGenerateTokenAndSendEmail()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "newuser@example.com" };
        var protectedToken = "protected_token_string";

        _dataProtectorMock.Setup(p => p.Protect(It.IsAny<byte[]>())).Returns(Encoding.UTF8.GetBytes(protectedToken));

        // Act
        var resultToken = await _emailVerificationService.GenerateAndSendVerificationEmailAsync(user);

        // Assert
        Assert.NotNull(resultToken);
        _sesServiceMock.Verify(s => s.SendEmailAsync(It.Is<SendEmailRequest>(
            req => req.Destination.ToAddresses.Contains(user.Email) &&
                   req.Message.Subject.Data == "Verify Your Bulldog Account" &&
                   req.Message.Body.Html.Data.Contains("localhost:3000/verify-email?token=")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailTokenAsync_ShouldReturnTrueAndVerifyUser_WhenTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEmail = "test@example.com";
        var user = new User { Id = userId, Email = userEmail, EmailVerified = false };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var expiry = DateTime.UtcNow.AddHours(1);
        var unprotectedToken = $"{userId}|{userEmail}|{expiry.Ticks}";

        _dataProtectorMock.Setup(dp => dp.Unprotect(It.IsAny<byte[]>()))
            .Returns(Encoding.UTF8.GetBytes(unprotectedToken));

        // Act
        var result = await _emailVerificationService.VerifyEmailTokenAsync("valid-token");

        // Assert
        Assert.True(result);
        var dbUser = await _context.Users.FindAsync(userId);
        Assert.NotNull(dbUser);
        Assert.True(dbUser.EmailVerified);
    }

    [Fact]
    public async Task VerifyEmailTokenAsync_ShouldReturnFalse_WhenTokenIsExpired()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEmail = "test@example.com";
        var expiry = DateTime.UtcNow.AddHours(-1); // Expired
        var unprotectedToken = $"{userId}|{userEmail}|{expiry.Ticks}";

        _dataProtectorMock.Setup(dp => dp.Unprotect(It.IsAny<byte[]>()))
            .Returns(Encoding.UTF8.GetBytes(unprotectedToken));

        // Act
        var result = await _emailVerificationService.VerifyEmailTokenAsync("expired-token");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyEmailTokenAsync_ShouldReturnFalse_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEmail = "test@example.com";
        var expiry = DateTime.UtcNow.AddHours(1);
        var unprotectedToken = $"{userId}|{userEmail}|{expiry.Ticks}";

        _dataProtectorMock.Setup(dp => dp.Unprotect(It.IsAny<byte[]>()))
            .Returns(Encoding.UTF8.GetBytes(unprotectedToken));

        // Act
        var result = await _emailVerificationService.VerifyEmailTokenAsync("user-not-found-token");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyEmailTokenAsync_ShouldReturnFalse_WhenEmailDoesNotMatch()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEmailInDb = "original@example.com";
        var userEmailInToken = "different@example.com";
        var user = new User { Id = userId, Email = userEmailInDb, EmailVerified = false };
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        var expiry = DateTime.UtcNow.AddHours(1);
        var unprotectedToken = $"{userId}|{userEmailInToken}|{expiry.Ticks}";

        _dataProtectorMock.Setup(dp => dp.Unprotect(It.IsAny<byte[]>()))
            .Returns(Encoding.UTF8.GetBytes(unprotectedToken));

        // Act
        var result = await _emailVerificationService.VerifyEmailTokenAsync("email-mismatch-token");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyEmailTokenAsync_ShouldReturnFalse_WhenTokenIsMalformed()
    {
        // Arrange
        var unprotectedToken = "just-one-part"; // Malformed

        _dataProtectorMock.Setup(dp => dp.Unprotect(It.IsAny<byte[]>()))
            .Returns(Encoding.UTF8.GetBytes(unprotectedToken));

        // Act
        var result = await _emailVerificationService.VerifyEmailTokenAsync("malformed-unprotected-token");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyEmailTokenAsync_ShouldReturnFalse_WhenUnprotectThrowsException()
    {
        // Arrange
        _dataProtectorMock.Setup(dp => dp.Unprotect(It.IsAny<byte[]>()))
            .Throws(new CryptographicException("Invalid token"));

        // Act
        var result = await _emailVerificationService.VerifyEmailTokenAsync("token-that-causes-exception");

        // Assert
        Assert.False(result);
    }
}
