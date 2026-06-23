using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SensorManagementService.Data;
using SensorManagementService.Kafka;
using SensorManagementService.Models;
using SensorManagementService.Services;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── JWT Auth ─────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key required");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "scada-auth-service",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "scada-platform",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = "role",
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// ── PostgreSQL ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<SensorMgmtDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── Redis ─────────────────────────────────────────────────────────────────────
var redisOpts = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
redisOpts.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOpts));

// ── Kafka + Services ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<SensorStatusProducer>();
builder.Services.AddSingleton<PoolManager>();
builder.Services.AddHostedService<SensorDataConsumer>();
builder.Services.AddHostedService<ConsensusResultConsumer>();
builder.Services.AddHostedService<HeartbeatMonitor>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── DB migration + seed ───────────────────────────────────────────────────────
for (int attempt = 1; attempt <= 10; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorMgmtDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.Sensors.AnyAsync())
        {
            db.Sensors.AddRange(
                new Sensor { SensorId = "sensor-01", Status = "ACTIVE", Quality = "GOOD",
                    ValueMin = 250, ValueMax = 400,
                    AlarmThresholds = """{"p1":350,"p2":370,"p3":390}""" },
                new Sensor { SensorId = "sensor-02", Status = "ACTIVE", Quality = "GOOD",
                    ValueMin = 250, ValueMax = 400,
                    AlarmThresholds = """{"p1":350,"p2":370,"p3":390}""" },
                new Sensor { SensorId = "sensor-03", Status = "ACTIVE", Quality = "GOOD",
                    ValueMin = 250, ValueMax = 400,
                    AlarmThresholds = """{"p1":350,"p2":370,"p3":390}""" },
                new Sensor { SensorId = "sensor-04", Status = "ACTIVE", Quality = "GOOD",
                    ValueMin = 250, ValueMax = 400,
                    AlarmThresholds = """{"p1":350,"p2":370,"p3":390}""" },
                new Sensor { SensorId = "sensor-05", Status = "ACTIVE", Quality = "GOOD",
                    ValueMin = 250, ValueMax = 400,
                    AlarmThresholds = """{"p1":350,"p2":370,"p3":390}""" },
                new Sensor { SensorId = "sensor-06", Status = "STANDBY", Quality = "GOOD",
                    ValueMin = 250, ValueMax = 400,
                    AlarmThresholds = """{"p1":350,"p2":370,"p3":390}""" }
            );
            await db.SaveChangesAsync();
            app.Logger.LogInformation("Seeded 6 sensors (5 ACTIVE + 1 STANDBY)");
        }
        break;
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("DB not ready (attempt {Attempt}/10): {Msg}", attempt, ex.Message);
        if (attempt == 10) throw;
        await Task.Delay(3000);
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
