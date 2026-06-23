using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SensorSimulator;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var gatewayUrl    = config["GatewayUrl"]    ?? "http://localhost:8080";
var smsUrl        = config["SmsUrl"]        ?? "http://localhost:8081"; // SensorManagementService direktno
var adminUsername = config["Auth:AdminUsername"] ?? "admin";
var adminPassword = config["Auth:AdminPassword"] ?? "admin123";
var aesKey        = config["Security:AesKey"]
    ?? throw new InvalidOperationException("Security:AesKey required");
var ecPrivateKey  = config["Security:EcdsaPrivateKey"]
    ?? throw new InvalidOperationException("Security:EcdsaPrivateKey required");

var sensorCfg = new SensorConfig();
config.GetSection("Sensor").Bind(sensorCfg);

var crypto = new Crypto(aesKey, ecPrivateKey);
using var http = new HttpClient { BaseAddress = new Uri(gatewayUrl) };

// ── Acquire sensor token ──────────────────────────────────────────────────────
string sensorToken = config["SensorToken"] ?? "";
if (string.IsNullOrWhiteSpace(sensorToken))
{
    Console.WriteLine($"Acquiring sensor token for {sensorCfg.SensorId}...");
    HttpResponseMessage? loginResp = null;
    for (int attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            loginResp = await http.PostAsJsonAsync("/api/auth/login",
                new { username = adminUsername, password = adminPassword });
            break;
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"Gateway not ready, retry {attempt}/10 in 3s...");
            await Task.Delay(3000);
        }
    }
    if (loginResp == null || !loginResp.IsSuccessStatusCode)
        throw new InvalidOperationException("Login failed");

    var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
    var accessToken = loginJson.GetProperty("accessToken").GetString()!;

    http.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

    var tokenResp = await http.PostAsJsonAsync("/api/auth/sensor-token",
        new { sensorId = sensorCfg.SensorId });
    if (!tokenResp.IsSuccessStatusCode)
        throw new InvalidOperationException("Could not obtain sensor token (need ADMIN role)");

    var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
    sensorToken = tokenJson.GetProperty("accessToken").GetString()!;
    Console.WriteLine("Sensor token acquired.");
}

http.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sensorToken);

// ── Konektuj se na SMS command hub ───────────────────────────────────────────
await using var commandListener = new SensorCommandListener(smsUrl, sensorCfg.SensorId, sensorToken);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await commandListener.StartAsync(cts.Token);

// ── Simulation loop ───────────────────────────────────────────────────────────
var rng = new Random();
long messageId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

Console.WriteLine($"Starting sensor simulation: {sensorCfg.SensorId} range=[{sensorCfg.ValueMin},{sensorCfg.ValueMax}]");

while (!cts.IsCancellationRequested)
{
    // Čekaj ako je STOP ili BLOCK aktivan
    try { await commandListener.WaitIfPausedAsync(cts.Token); }
    catch (OperationCanceledException) { break; }

    var delayMs = rng.Next(1000, 5001);
    try { await Task.Delay(delayMs, cts.Token); } catch (OperationCanceledException) { break; }

    // Provjeri ponovo nakon delay-a (mogao je stići STOP tokom delay-a)
    if (!commandListener.IsRunning) continue;

    var value = sensorCfg.ValueMin + rng.NextDouble() * (sensorCfg.ValueMax - sensorCfg.ValueMin);
    var alarmPriority = sensorCfg.GetAlarmPriority(value);
    var now = DateTimeOffset.UtcNow;
    var timestamp = now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    var (signature, encryptedPayload) = crypto.SignAndEncrypt(
        sensorCfg.SensorId, messageId, timestamp, value, alarmPriority);

    var payload = new
    {
        sensorId = sensorCfg.SensorId,
        messageId,
        timestamp,
        value,
        alarmPriority,
        signature,
        encryptedPayload
    };

    int statusCode = 0;
    try
    {
        var resp = await http.PostAsJsonAsync("/api/ingest", payload);
        statusCode = (int)resp.StatusCode;
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine($"HTTP error: {ex.Message}");
        Console.ResetColor();
    }

    ConsoleDisplay.Print(sensorCfg.SensorId, value, now, alarmPriority, statusCode);
    messageId++;
}

Console.WriteLine("Simulator stopped.");
