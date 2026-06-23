using System.Text.Json;

namespace Scada.Shared.Security;

public static class CanonicalJson
{
    // Produces a deterministic JSON representation for signing/verification.
    // Field order matches the spec: sensorId, messageId, timestamp, value, alarmPriority.
    public static string Build(string sensorId, long messageId, string timestamp, double value, int alarmPriority)
    {
        return JsonSerializer.Serialize(new
        {
            sensorId,
            messageId,
            timestamp,
            value,
            alarmPriority
        });
    }
}
