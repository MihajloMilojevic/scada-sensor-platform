# Shared Libraries

Three class libraries shared across services via project references.

## Scada.Shared.Contracts

Kafka message DTOs and shared enums.

### Enums

```csharp
public enum SensorQuality { GOOD, BAD, UNCERTAIN }
public enum SensorStatus  { ACTIVE, INACTIVE, STANDBY }
```

### Kafka Message DTOs

**`SensorDataMessage`** (topic `sensor.data`)
```csharp
public record SensorDataMessage(
    string SensorId, double Value, DateTime Timestamp,
    int AlarmPriority, SensorQuality Quality);
```

**`SensorStatusMessage`** (topic `sensor.status`)
```csharp
public record SensorStatusMessage(
    string SensorId, SensorStatus Status, SensorStatus PreviousStatus,
    string Reason, DateTime Timestamp);
```

**`ConsensusResultMessage`** (topic `consensus.result`)
```csharp
public record ConsensusResultMessage(
    string SensorId, SensorQuality PreviousQuality, SensorQuality NewQuality,
    double ConsensusValue, double SensorValue, double DeviationSigma, DateTime Timestamp);
```

## Scada.Shared.Kafka

Thin wrappers around `Confluent.Kafka` with JSON serialization.

### `KafkaProducer<T>`

```csharp
// Serializes T to JSON with JsonStringEnumConverter (enums as strings, not ints)
await producer.PublishAsync(topic, key, message);
```

### `KafkaConsumer<T>`

```csharp
// BackgroundService base; deserializes with JsonStringEnumConverter
protected abstract Task HandleAsync(T message, CancellationToken ct);
```

> **Important:** Both producer and consumer use `JsonStringEnumConverter`. Enum values are serialized as `"GOOD"` / `"ACTIVE"` etc. — not as integers. Both sides must use the same serializer.

## Scada.Shared.Security

Cryptographic primitives for sensor message security.

### `AesMessageCipher`

AES-256-GCM encrypt/decrypt of a JSON string payload.

```csharp
string ciphertext = AesMessageCipher.Encrypt(plaintext, base64AesKey);
string plaintext  = AesMessageCipher.Decrypt(ciphertext, base64AesKey);
```

Key format: 32-byte key, base64-encoded.

### `MessageSigner` / `MessageVerifier`

ECDSA P-256 sign and verify over the SHA-256 digest of canonical JSON.

```csharp
// Signing (simulator):
string sig = MessageSigner.Sign(canonicalJson, base64Pkcs8PrivateKey);

// Verification (ingestion):
bool ok = MessageVerifier.Verify(canonicalJson, base64Signature, base64Pkcs8PublicKey);
```

Key format: PKCS#8 DER, base64-encoded.

### `ReplayGuard`

In-memory replay protection. Stores `(lastMsgId, lastTimestamp)` per sensor in a `ConcurrentDictionary`.

```csharp
bool valid = replayGuard.IsValid(sensorId, messageId, timestamp, windowSeconds: 30);
```

Returns `false` if `messageId` was already seen within `windowSeconds` of `timestamp`, or if `timestamp` is older than `windowSeconds` from now.

### `CanonicalJson`

Serializes a dictionary to JSON with keys sorted alphabetically and no whitespace. Used to ensure both the signing side and the verification side hash the same byte sequence.

```csharp
string canonical = CanonicalJson.Serialize(new Dictionary<string,object> {
    {"alarmPriority", 0}, {"messageId", 4821}, {"sensorId", "sensor-01"},
    {"timestamp", "2026-06-14T10:15:30.123Z"}, {"value", 312.7}
});
// → {"alarmPriority":0,"messageId":4821,"sensorId":"sensor-01","timestamp":"...","value":312.7}
```
