using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Dispatch;
using NotificationService.Hubs;
using NotificationService.Kafka;

var builder = WebApplication.CreateBuilder(args);

// ── JWT ───────────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key required");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.MapInboundClaims = false;
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer   = true,
            ValidIssuer      = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience    = builder.Configuration["Jwt:Audience"],
            RoleClaimType    = "role",
            NameClaimType    = "sub"
        };
        // SignalR sends token via query param for WS connections
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── CORS (Angular dev server) ─────────────────────────────────────────────────
builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// ── Dispatch ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<RateLimiter>();
builder.Services.AddSingleton<Dispatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RateLimiter>());
builder.Services.AddHostedService<SensorDataConsumer>();
builder.Services.AddHostedService<SensorStatusConsumer>();
builder.Services.AddHostedService<ConsensusResultConsumer>();

var app = builder.Build();

app.UseWebSockets();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<NotificationHub>("/ws/notifications");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
