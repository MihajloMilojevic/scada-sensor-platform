using IngestionService.Models;
using Scada.Shared.Contracts;
using Scada.Shared.Security;
using System.Text.Json;

namespace IngestionService.Pipeline;

public class MessageValidator(IConfiguration config, ReplayGuard replayGuard)
{
    private readonly byte[] _aesKey = Convert.FromBase64String(
        config["Security:AesKey"] ?? throw new InvalidOperationException("Security:AesKey required"));
    private readonly byte[] _ecdsaPublicKey = Convert.FromBase64String(
        config["Security:EcdsaPublicKey"] ?? throw new InvalidOperationException("Security:EcdsaPublicKey required"));

    public (bool Valid, string? Error, SensorDataMessage? Message) Validate(IngestRequest req, string jwtSub)
    {
        // Verify the JWT subject matches the sensorId claim
        if (!string.Equals(jwtSub, req.SensorId, StringComparison.OrdinalIgnoreCase))
            return (false, "SensorId in request does not match JWT sub claim", null);

        // Decrypt encryptedPayload → canonical JSON
        string canonicalJson;
        try { canonicalJson = AesMessageCipher.Decrypt(req.EncryptedPayload, _aesKey); }
        catch (Exception ex) { return (false, $"Decryption failed: {ex.Message}", null); }

        // Verify ECDSA signature over canonical JSON
        if (!MessageVerifier.Verify(canonicalJson, req.Signature, _ecdsaPublicKey))
            return (false, "Invalid signature", null);

        // Parse canonical JSON to get trusted values
        JsonElement trusted;
        try
        {
            trusted = JsonSerializer.Deserialize<JsonElement>(canonicalJson);
        }
        catch { return (false, "Invalid canonical JSON", null); }

        var trustedSensorId = trusted.GetProperty("sensorId").GetString() ?? "";
        var trustedMsgId = trusted.GetProperty("messageId").GetInt64();
        var trustedTimestamp = trusted.GetProperty("timestamp").GetString() ?? "";
        var trustedValue = trusted.GetProperty("value").GetDouble();
        var trustedAlarmPriority = trusted.GetProperty("alarmPriority").GetInt32();

        // Replay guard (uses trusted values)
        if (!DateTimeOffset.TryParse(trustedTimestamp, out var ts))
            return (false, "Invalid timestamp in payload", null);

        if (!replayGuard.IsValid(trustedSensorId, trustedMsgId, ts.UtcDateTime))
            return (false, "Replay detected or stale timestamp", null);

        var message = new SensorDataMessage
        {
            SensorId = trustedSensorId,
            Value = trustedValue,
            Timestamp = ts,
            AlarmPriority = trustedAlarmPriority,
            Quality = SensorQuality.GOOD
        };
        return (true, null, message);
    }
}
