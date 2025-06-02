using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using backend.Models;
using backend.Services.Auth.Implementations;
using backend.Services.Auth.Interfaces;
using Microsoft.Extensions.Configuration;

namespace backend.Tests.Services;

public class JwtServiceTests
{
    private readonly IJwtService _jwtService;
    private readonly string _jwtSecret = "supersecretkey1234567890dddddddddddddddddddddddddddddddddd!"; // must be long enough
    private readonly int _jwtLifespanMinutes = 1;

    public JwtServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?> {
            { "Jwt:Secret", _jwtSecret },
            { "Jwt:LifespanMinutes", _jwtLifespanMinutes.ToString() }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _jwtService = new JwtService(configuration);
    }

    [Fact]
    public void GenerateToken_ShouldIncludeExpectedClaims()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        // Act
        var token = _jwtService.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // Assert
        Assert.NotNull(token);
        Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Email && c.Value == user.Email);
        Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        Assert.Contains(jwtToken.Claims, c => c.Type == "displayName" && c.Value == user.DisplayName);
    }

    [Fact]
    public void ValidateToken_ValidToken_ShouldReturnPrincipal()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "valid@example.com",
            DisplayName = "Valid User"
        };
        var token = _jwtService.GenerateToken(user);

        // Act
        var principal = _jwtService.ValidateToken(token);

        // Assert
        Assert.NotNull(principal);
        Assert.Equal(user.Email, principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal(user.Id.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(user.DisplayName, principal.FindFirst("displayName")?.Value);
    }

    [Fact]
    public void ValidateToken_InvalidToken_ShouldReturnNull()
    {
        // Arrange
        var invalidToken = "this.is.not.a.valid.token";

        // Act
        var principal = _jwtService.ValidateToken(invalidToken);

        // Assert
        Assert.Null(principal);
    }
}
