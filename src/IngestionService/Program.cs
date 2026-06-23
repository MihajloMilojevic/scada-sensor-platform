using IngestionService.InfluxDb;
using IngestionService.Kafka;
using IngestionService.Pipeline;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scada.Shared.Security;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── JWT Auth (validates ingest:write scope) ───────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key required");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

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
            IssuerSigningKey = signingKey,
            RoleClaimType = "role",
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(opt =>
    opt.AddPolicy("IngestWrite", p => p.RequireClaim("scope", "ingest:write")));

// ── Pipeline services ─────────────────────────────────────────────────────────
builder.Services.AddSingleton<ReplayGuard>();
builder.Services.AddSingleton<MessageValidator>();
builder.Services.AddSingleton<WriteAheadLog>();
builder.Services.AddSingleton<RotationManager>();
builder.Services.AddSingleton<InfluxWriter>();
builder.Services.AddSingleton<SensorDataProducer>();
builder.Services.AddHostedService<FlushWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── WAL recovery on startup ───────────────────────────────────────────────────
var walService = app.Services.GetRequiredService<WriteAheadLog>();
var rotationMgr = app.Services.GetRequiredService<RotationManager>();
var recoveryMessages = walService.LoadForRecovery().ToList();
if (recoveryMessages.Count > 0)
{
    app.Logger.LogInformation("WAL recovery: loading {Count} messages", recoveryMessages.Count);
    foreach (var msg in recoveryMessages)
        rotationMgr.Add(msg);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
