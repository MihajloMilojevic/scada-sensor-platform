using System;
using ApiGateway.Middleware;
using ApiGateway.Services;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ── Redis (gateway token cache) ───────────────────────────────────────────────
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redisOpts = ConfigurationOptions.Parse(redisConn);
redisOpts.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOpts));
builder.Services.AddSingleton<TokenCacheService>();

// ── HTTP client for auth-service calls ────────────────────────────────────────
builder.Services.AddHttpClient("auth");

// ── YARP reverse proxy ────────────────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ── Rate limiter: 10 req/s keyed by JWT sub claim ─────────────────────────────
var permitLimit = int.Parse(builder.Configuration["RateLimit:PermitLimit"] ?? "10");
var windowSec = int.Parse(builder.Configuration["RateLimit:WindowSeconds"] ?? "1");

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = 429;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests" }, ct);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpCtx =>
    {
        var path = httpCtx.Request.Path.Value ?? "";
        // Skip rate limiting for unauthenticated auth endpoints
        if (path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase))
            return RateLimitPartition.GetNoLimiter("bypass");

        var sub = httpCtx.Items.TryGetValue("sub", out var s) && s != null
            ? s.ToString()!
            : httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(sub, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSec),
            QueueLimit = 0
        });
    });
});

const string CorsPolicyName = "_allowAll";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseCors(CorsPolicyName);
app.UseWebSockets();
app.UseMiddleware<JwtCacheMiddleware>();
app.UseRateLimiter();
app.MapReverseProxy();

app.Run();