using System.Text;
using AnalyticsService.Composition;
using AnalyticsService.Data;
using AnalyticsService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── EF Core ──────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AnalyticsDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

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
    });
builder.Services.AddAuthorization();

// ── HTTP client for upstream service calls ────────────────────────────────────
builder.Services.AddHttpClient("upstream");

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ServiceTokenProvider>();
builder.Services.AddScoped<ReportComposer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── DB migration ──────────────────────────────────────────────────────────────
for (int attempt = 1; attempt <= 10; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>().Database.MigrateAsync();
        app.Logger.LogInformation("Database migration complete");
        break;
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Migration attempt {A}/10: {Msg}", attempt, ex.Message);
        if (attempt == 10) throw;
        await Task.Delay(3000);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
