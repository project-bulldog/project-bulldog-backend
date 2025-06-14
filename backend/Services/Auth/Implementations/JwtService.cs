using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Models;
using backend.Services.Auth.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace backend.Services.Auth.Implementations;

public class JwtService : IJwtService
{
    private readonly string _jwtSecret;
    private readonly int _jwtLifespanMinutes;

    public JwtService(IConfiguration config)
    {
        _jwtSecret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Missing JWT Secret");
        _jwtLifespanMinutes = int.Parse(config["Jwt:LifespanMinutes"] ?? "15");
    }

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("displayName", user.DisplayName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtLifespanMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSecret);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                NameClaimType = JwtRegisteredClaimNames.Sub
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
