using System;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace AuthService.Services;

public class JwtTokenService
{
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessExpiryMinutes;
    private readonly int _sensorExpiryDays;
    private readonly int _refreshExpiryDays;

    public JwtTokenService(IConfiguration config)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key required");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        _issuer = config["Jwt:Issuer"] ?? "scada-auth-service";
        _audience = config["Jwt:Audience"] ?? "scada-platform";
        _accessExpiryMinutes = int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15");
        _sensorExpiryDays = int.Parse(config["Jwt:SensorTokenExpiryDays"] ?? "365");
        _refreshExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7");
    }

    public string CreateAccessToken(string userId, string role)
    {
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("role", role),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        return BuildToken(claims, TimeSpan.FromMinutes(_accessExpiryMinutes));
    }

    public string CreateSensorToken(string sensorId)
    {
        var claims = new[]
        {
            new Claim("sub", sensorId),
            new Claim("role", "SENSOR"),
            new Claim("scope", "ingest:write"),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        return BuildToken(claims, TimeSpan.FromDays(_sensorExpiryDays));
    }

    public (string Token, string TokenId) CreateRefreshToken(string userId)
    {
        var tokenId = Guid.NewGuid().ToString();
        var claims = new[]
        {
            new Claim("sub", userId),
            new Claim("jti", tokenId),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        return (BuildToken(claims, TimeSpan.FromDays(_refreshExpiryDays)), tokenId);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            // Clear mapping so JWT claim names are preserved as-is (sub stays sub, not NameIdentifier)
            handler.InboundClaimTypeMap.Clear();
            return handler.ValidateToken(token, BuildValidationParams(), out _);
        }
        catch
        {
            return null;
        }
    }

    public (string UserId, string TokenId)? ParseRefreshToken(string token)
    {
        var principal = ValidateToken(token);
        if (principal == null) return null;
        var userId = principal.FindFirst("sub")?.Value;
        var tokenId = principal.FindFirst("jti")?.Value;
        return userId != null && tokenId != null ? (userId, tokenId) : null;
    }

    public TokenValidationParameters BuildValidationParams() => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = _issuer,
        ValidAudience = _audience,
        IssuerSigningKey = _signingKey,
        RoleClaimType = "role",
        NameClaimType = "sub",
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    private string BuildToken(Claim[] claims, TimeSpan expiry)
    {
        var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}