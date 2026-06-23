using System;
using System.Threading.Tasks;
using AuthService.Data;
using AuthService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────
// ────────────────────────────────────────────────
builder.Services.AddDbContext<AuthDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── Redis ─────────────────────────────────────────────────────────────────────
var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redisOpts = ConfigurationOptions.Parse(redisConn);
redisOpts.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOpts));

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<RefreshTokenStore>();

// ── JWT Auth ──────────────────────────────────────────────────────────────────
// Build a temporary JwtTokenService just to get the validation params
var tempConfig = builder.Configuration;
var tempJwtService = new JwtTokenService(tempConfig);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = tempJwtService.BuildValidationParams();
        opt.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Migrate DB on startup (with retry for docker startup order) ───────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch (Exception ex) when (attempt < 10)
        {
            app.Logger.LogWarning("DB not ready (attempt {Attempt}): {Msg}", attempt, ex.Message);
            await Task.Delay(3000);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();