using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AnalyticsService.Services;

public class ServiceTokenProvider(IConfiguration config)
{
    private string? _cachedToken;
    private DateTimeOffset _expires = DateTimeOffset.MinValue;

    public string GetToken()
    {
        if (_cachedToken != null && DateTimeOffset.UtcNow < _expires.AddMinutes(-1))
            return _cachedToken;

        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key required");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var expiry = DateTime.UtcNow.AddHours(1);
        var token = new JwtSecurityToken(
            issuer:   config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:   [new Claim("sub", "analytics-service"), new Claim("role", "ADMIN")],
            expires:  expiry,
            signingCredentials: credentials);

        _cachedToken = new JwtSecurityTokenHandler().WriteToken(token);
        _expires = new DateTimeOffset(expiry, TimeSpan.Zero);
        return _cachedToken;
    }
}
