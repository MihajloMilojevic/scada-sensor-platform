using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using ApiGateway.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.Middleware;

public class JwtCacheMiddleware(RequestDelegate next, TokenCacheService cache, IHttpClientFactory httpFactory, IConfiguration config)
{
    private static readonly HashSet<string> AnonymousPaths =
    [
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/ingest/health"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        // Let CORS preflight pass through — handled by UseCors() upstream
        if (context.Request.Method == HttpMethods.Options)
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";

        // Let unauthenticated endpoints pass through directly
        if (AnonymousPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        // SignalR WS connections send the token via query param
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var qToken = context.Request.Query["access_token"].ToString();
            if (!string.IsNullOrEmpty(qToken))
                authHeader = $"Bearer {qToken}";
        }
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid Authorization header" });
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var tokenHash = ComputeHash(token);

        // Check cache
        var cached = await cache.GetAsync(tokenHash);
        if (cached != null)
        {
            InjectClaims(context, cached);
            await next(context);
            return;
        }

        // Cache miss — validate via Auth Service
        var authServiceUrl = config["AuthService:BaseUrl"] ?? "http://auth-service:8080";
        var client = httpFactory.CreateClient("auth");
        var req = new HttpRequestMessage(HttpMethod.Get, $"{authServiceUrl}/api/auth/verify");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage res;
        try { res = await client.SendAsync(req); }
        catch
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "Auth service unavailable" });
            return;
        }

        if (!res.IsSuccessStatusCode)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        var body = await res.Content.ReadAsStringAsync();
        CachedClaims? claims;
        try { claims = JsonSerializer.Deserialize<CachedClaims>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch { claims = null; }

        if (claims == null)
        {
            context.Response.StatusCode = 401;
            return;
        }

        var remaining = DateTimeOffset.FromUnixTimeSeconds(claims.Exp) - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
            await cache.SetAsync(tokenHash, claims, remaining);

        InjectClaims(context, claims);
        await next(context);
    }

    private static void InjectClaims(HttpContext context, CachedClaims claims)
    {
        context.Items["sub"] = claims.Sub;
        context.Items["role"] = claims.Role;
        context.Items["scope"] = claims.Scope;
    }

    private static string ComputeHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}