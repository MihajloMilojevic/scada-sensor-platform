using System;
using Scada.Shared.Security;

namespace SensorSimulator;

public class Crypto
{
    private readonly byte[] _aesKey;
    private readonly byte[] _ecPrivateKey;

    public Crypto(string aesKeyBase64, string ecPrivateKeyBase64)
    {
        _aesKey = Convert.FromBase64String(aesKeyBase64);
        _ecPrivateKey = Convert.FromBase64String(ecPrivateKeyBase64);
    }

    public (string Signature, string EncryptedPayload) SignAndEncrypt(
        string sensorId, long messageId, string timestamp, double value, int alarmPriority)
    {
        var canonical = CanonicalJson.Build(sensorId, messageId, timestamp, value, alarmPriority);
        var signature = MessageSigner.Sign(canonical, _ecPrivateKey);
        var encrypted = AesMessageCipher.Encrypt(canonical, _aesKey);
        return (signature, encrypted);
    }
}