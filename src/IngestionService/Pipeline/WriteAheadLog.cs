using Scada.Shared.Contracts;
using System.Text.Json;

namespace IngestionService.Pipeline;

public class WriteAheadLog(IConfiguration config, ILogger<WriteAheadLog> logger)
{
    private readonly string _walDir = config["Wal:Directory"] ?? "/data/wal";
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task AppendAsync(SensorDataMessage msg)
    {
        Directory.CreateDirectory(_walDir);
        var path = WalPath(msg.SensorId);
        var line = JsonSerializer.Serialize(msg) + Environment.NewLine;
        await _lock.WaitAsync();
        try { await File.AppendAllTextAsync(path, line); }
        finally { _lock.Release(); }
    }

    // Atomically rename current WAL file to .flushing so new writes go to a fresh file.
    // Returns the path of the segment to be flushed (null if nothing to flush).
    public string? RotateToFlushing(string sensorId)
    {
        var activePath = WalPath(sensorId);
        if (!File.Exists(activePath)) return null;
        var flushingPath = activePath + ".flushing";
        File.Move(activePath, flushingPath, overwrite: true);
        return flushingPath;
    }

    public void DeleteSegment(string flushingPath)
    {
        try { File.Delete(flushingPath); }
        catch (Exception ex) { logger.LogWarning("Could not delete WAL segment {Path}: {Msg}", flushingPath, ex.Message); }
    }

    // On startup: load all unprocessed WAL entries for recovery.
    public IEnumerable<SensorDataMessage> LoadForRecovery()
    {
        if (!Directory.Exists(_walDir)) yield break;
        var files = Directory.GetFiles(_walDir, "*.jsonl")
            .Concat(Directory.GetFiles(_walDir, "*.jsonl.flushing"));

        foreach (var file in files)
        {
            foreach (var line in File.ReadAllLines(file))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                SensorDataMessage? msg = null;
                try { msg = JsonSerializer.Deserialize<SensorDataMessage>(line); } catch { }
                if (msg != null) yield return msg;
            }
            // Remove recovery file after loading
            try { File.Delete(file); } catch { }
        }
    }

    public string WalPath(string sensorId) => Path.Combine(_walDir, $"{sensorId}.jsonl");
}
