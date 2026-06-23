using Microsoft.AspNetCore.SignalR.Client;

namespace SensorSimulator;

/// <summary>
/// Konektuje se na SensorManagementService SignalR hub.
/// 
/// Flow:
///   - Pri konekciji: šalje READY, čeka START ili STOP od SMS-a
///   - START  → počinje slanje
///   - STOP   → pauzira, čeka START
///   - BLOCK  → spava N sekundi, pa šalje READY i čeka odluku SMS-a
/// </summary>
public class SensorCommandListener : IAsyncDisposable
{
    private readonly HubConnection _hub;
    private readonly string _sensorId;
    private bool _connected = false;

    private TaskCompletionSource _resumeTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _running = false; // Inicijalno false — čekamo READY odgovor

    public SensorCommandListener(string smsBaseUrl, string sensorId, string sensorToken)
    {
        _sensorId = sensorId;

        _hub = new HubConnectionBuilder()
            .WithUrl($"{smsBaseUrl}/hubs/sensor-commands?sensorId={Uri.EscapeDataString(sensorId)}&access_token={sensorToken}")
            .WithAutomaticReconnect(new[] {
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _hub.On<string, int>("Command", HandleCommand);

        _hub.Reconnected += async _ =>
        {
            _connected = true;
            Log("Reconnected — sending READY");
            await SendReadyAsync();
        };

        _hub.Closed += _ =>
        {
            _connected = false;
            Log("Command hub connection closed");
            return Task.CompletedTask;
        };

        // Inicijalno zaključano — čekamo READY odgovor
        // _resumeTcs ostaje nezavršen
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
            await _hub.StartAsync(linked.Token);
            _connected = true;
            Log("Connected to command hub — sending READY");
            await SendReadyAsync();
        }
        catch (Exception ex)
        {
            // Graceful degradation — radi standalone ako SMS nije dostupan
            Log($"Command hub unavailable ({ex.GetType().Name}: {ex.Message}) — running standalone");
            _running = true;
            _resumeTcs.TrySetResult();
        }
    }

    private async Task SendReadyAsync()
    {
        try
        {
            await _hub.InvokeAsync("Ready", _sensorId);
            Log("→ READY sent");
        }
        catch (Exception ex)
        {
            Log($"Failed to send READY: {ex.Message} — running standalone");
            _running = true;
            _resumeTcs.TrySetResult();
        }
    }

    public Task WaitIfPausedAsync(CancellationToken ct)
    {
        if (_resumeTcs.Task.IsCompleted) return Task.CompletedTask;
        return _resumeTcs.Task.WaitAsync(ct);
    }

    public bool IsRunning => _running;
    public bool IsConnected => _connected;

    private void HandleCommand(string command, int durationSeconds)
    {
        Log($"← CMD {command}{(durationSeconds > 0 ? $" ({durationSeconds}s)" : "")}");

        switch (command)
        {
            case "START":
                _running = true;
                _resumeTcs.TrySetResult();
                break;

            case "STOP":
                _running = false;
                // Zamijeni TCS novim ako je prethodni završen
                if (_resumeTcs.Task.IsCompleted)
                    _resumeTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Log("Paused — waiting for START or READY response");
                break;

            case "BLOCK":
                _running = false;
                if (_resumeTcs.Task.IsCompleted)
                    _resumeTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Log($"Blocked for {durationSeconds}s — will send READY when done");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
                    Log("Block expired — sending READY");
                    if (_connected)
                        await SendReadyAsync();
                    else
                    {
                        // SMS nedostupan, nastavi standalone
                        _running = true;
                        _resumeTcs.TrySetResult();
                    }
                });
                break;

            default:
                Log($"Unknown command: {command}");
                break;
        }
    }

    private void Log(string msg) =>
        Console.WriteLine($"[{_sensorId}][HUB] {msg}");

    public async ValueTask DisposeAsync()
    {
        try { await _hub.DisposeAsync(); }
        catch { /* ignore on shutdown */ }
    }
}
