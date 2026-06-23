using System;
using System.Threading.Tasks;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthDbContext db, JwtTokenService jwt, RefreshTokenStore refreshStore) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == req.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        var accessToken = jwt.CreateAccessToken(user.Id.ToString(), user.Role);
        var (refreshToken, tokenId) = jwt.CreateRefreshToken(user.Id.ToString());
        await refreshStore.StoreAsync(user.Id.ToString(), tokenId);

        return Ok(new { accessToken, refreshToken });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var parsed = jwt.ParseRefreshToken(req.RefreshToken);
        if (parsed == null)
            return Unauthorized(new { error = "Invalid refresh token" });

        var (userId, tokenId) = parsed.Value;
        var result = await refreshStore.UseTokenAsync(userId, tokenId);

        if (result == 2)
        {
            // Reuse detected — revoke all sessions for this user
            await refreshStore.RevokeAllAsync(userId);
            return Unauthorized(new { error = "Refresh token reuse detected — all sessions revoked" });
        }

        if (result == 0)
            return Unauthorized(new { error = "Refresh token not found or expired" });

        // Issue new pair
        var user = await db.Users.FindAsync(Guid.Parse(userId));
        if (user == null)
            return Unauthorized(new { error = "User not found" });

        var accessToken = jwt.CreateAccessToken(userId, user.Role);
        var (newRefreshToken, newTokenId) = jwt.CreateRefreshToken(userId);
        await refreshStore.StoreAsync(userId, newTokenId);

        return Ok(new { accessToken, refreshToken = newRefreshToken });
    }

    [HttpGet("verify")]
    [Authorize]
    public IActionResult Verify()
    {
        var sub = User.FindFirst("sub")?.Value ?? "";
        var role = User.FindFirst("role")?.Value ?? "";
        var scope = User.FindFirst("scope")?.Value;
        long.TryParse(User.FindFirst("exp")?.Value, out var exp);

        return Ok(new { sub, role, scope, exp });
    }

    [HttpPost("sensor-token")]
    [Authorize(Roles = "ADMIN")]
    public IActionResult SensorToken([FromBody] SensorTokenRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SensorId))
            return BadRequest(new { error = "sensorId is required" });

        var accessToken = jwt.CreateSensorToken(req.SensorId);
        return Ok(new { accessToken, tokenType = "Bearer", scope = "ingest:write" });
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "ok", service = "AuthService" });
}

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record SensorTokenRequest(string SensorId);