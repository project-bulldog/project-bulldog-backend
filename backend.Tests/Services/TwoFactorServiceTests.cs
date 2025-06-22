using Amazon.SimpleEmail;
using backend.Data;
using backend.Enums;
using backend.Models;
using backend.Services.Auth.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests.Services;

public class TwoFactorServiceTests : IDisposable
{
    private readonly Mock<IAmazonSimpleEmailService> _sesServiceMock;
    private readonly Mock<ILogger<TwoFactorService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly BulldogDbContext _context;
    private readonly TwoFactorService _twoFactorService;

    public TwoFactorServiceTests()
    {
        _sesServiceMock = new Mock<IAmazonSimpleEmailService>();
        _loggerMock = new Mock<ILogger<TwoFactorService>>();
        _configurationMock = new Mock<IConfiguration>();

        // Setup configuration for fake SMS service (no Twilio)
        _configurationMock.Setup(x => x["Twilio:AccountSid"]).Returns("");
        _configurationMock.Setup(x => x["Twilio:AuthToken"]).Returns("");
        _configurationMock.Setup(x => x["Twilio:FromNumber"]).Returns("");
        _configurationMock.Setup(x => x["AWS:FromEmail"]).Returns("test@example.com");

        var options = new DbContextOptionsBuilder<BulldogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BulldogDbContext(options);

        _twoFactorService = new TwoFactorService(
            _context,
            _loggerMock.Object,
            _configurationMock.Object,
            _sesServiceMock.Object
        );
    }

    [Fact]
    public async Task GenerateAndSendOtpAsync_ShouldGenerateAndSendOtp()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            PhoneNumber = "+1234567890"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _sesServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<Amazon.SimpleEmail.Model.SendEmailRequest>(), default))
            .ReturnsAsync(new Amazon.SimpleEmail.Model.SendEmailResponse());

        // Act
        var otp = await _twoFactorService.GenerateAndSendOtpAsync(user);

        // Assert
        Assert.NotNull(otp);
        Assert.Equal(6, otp.Length);
        Assert.True(otp.All(char.IsDigit));

        // Verify user was updated
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(otp, updatedUser!.CurrentOtp);
        Assert.NotNull(updatedUser.OtpExpiresAt);
        Assert.Equal(5, updatedUser.OtpAttemptsLeft);
    }

    [Fact]
    public async Task GenerateAndSendOtpAsync_WithSpecificMethod_ShouldUseSpecifiedMethod()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            PhoneNumber = "+1234567890"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _sesServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<Amazon.SimpleEmail.Model.SendEmailRequest>(), default))
            .ReturnsAsync(new Amazon.SimpleEmail.Model.SendEmailResponse());

        // Act
        var otp = await _twoFactorService.GenerateAndSendOtpAsync(user, OtpDeliveryMethod.Sms);

        // Assert
        Assert.NotNull(otp);
        Assert.Equal(6, otp.Length);

        // Verify user was updated
        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(otp, updatedUser!.CurrentOtp);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithValidCode_ShouldReturnTrue()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            CurrentOtp = "123456",
            OtpExpiresAt = DateTime.UtcNow.AddMinutes(5),
            OtpAttemptsLeft = 5,
            PhoneNumberVerified = false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _twoFactorService.VerifyOtpAsync(user, "123456");

        // Assert
        Assert.True(result);

        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Null(updatedUser!.CurrentOtp);
        Assert.Null(updatedUser.OtpExpiresAt);
        Assert.Equal(5, updatedUser.OtpAttemptsLeft);
        Assert.True(updatedUser.PhoneNumberVerified);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithInvalidCode_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            CurrentOtp = "123456",
            OtpExpiresAt = DateTime.UtcNow.AddMinutes(5),
            OtpAttemptsLeft = 5
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _twoFactorService.VerifyOtpAsync(user, "654321");

        // Assert
        Assert.False(result);

        var updatedUser = await _context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("123456", updatedUser!.CurrentOtp); // OTP should remain unchanged
        Assert.Equal(4, updatedUser.OtpAttemptsLeft); // Attempts should be decremented
    }

    [Fact]
    public async Task VerifyOtpAsync_WithExpiredCode_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            CurrentOtp = "123456",
            OtpExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired
            OtpAttemptsLeft = 5
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _twoFactorService.VerifyOtpAsync(user, "123456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithNoOtp_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            CurrentOtp = null,
            OtpExpiresAt = DateTime.UtcNow.AddMinutes(5),
            OtpAttemptsLeft = 5
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _twoFactorService.VerifyOtpAsync(user, "123456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithNoExpiry_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            CurrentOtp = "123456",
            OtpExpiresAt = null,
            OtpAttemptsLeft = 5
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _twoFactorService.VerifyOtpAsync(user, "123456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyOtpAsync_WithNoAttemptsLeft_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User",
            CurrentOtp = "123456",
            OtpExpiresAt = DateTime.UtcNow.AddMinutes(5),
            OtpAttemptsLeft = 0 // No attempts left
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _twoFactorService.VerifyOtpAsync(user, "123456");

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
