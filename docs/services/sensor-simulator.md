# SensorSimulator

Simulates a physical sensor. Generates random values, computes ECDSA-P256 signatures, encrypts with AES-256-GCM, and POSTs to the IngestionService every 1–5 seconds. Five instances run as ACTIVE sensors; a sixth (sensor-06) is STANDBY.

## Startup Sequence

1. Reads `SensorToken` from config. If empty, logs in as admin and calls `POST /api/auth/sensor-token` to obtain one, then caches it in memory.
2. Enters send loop: generate → sign → encrypt → POST → wait random 1–5 s.

## Alarm Thresholds

| Priority | Default threshold | Console colour |
|----------|------------------|----------------|
| P1 (Warning) | value ≥ 350 | Yellow |
| P2 (High) | value ≥ 370 | Orange |
| P3 (Critical) | value ≥ 390 | Red |

Priority 3 supersedes 1 and 2; otherwise the highest matching threshold wins.

Value range default: **250–400** (configurable per sensor).

## Cryptography

Uses `Scada.Shared.Security`:

- **AES-256-GCM** — `AesMessageCipher.Encrypt(canonicalJson)` → base64 ciphertext
- **ECDSA P-256** — `MessageSigner.Sign(canonicalJson)` → base64 signature
- **Canonical JSON** — fields sorted alphabetically, no whitespace: `{"alarmPriority":0,"messageId":4821,"sensorId":"sensor-01","timestamp":"...","value":312.7}`

## Configuration

```json
{
  "GatewayUrl": "http://api-gateway:8080",
  "SensorToken": "",
  "Auth": { "AdminUsername": "admin", "AdminPassword": "admin123" },
  "Security": {
    "AesKey": "<base64 AES-256 key>",
    "EcdsaPrivateKey": "<base64 PKCS8 ECDSA-P256 private key>"
  },
  "Sensor": {
    "SensorId": "sensor-01",
    "ValueMin": 250,
    "ValueMax": 400,
    "AlarmP1Threshold": 350,
    "AlarmP2Threshold": 370,
    "AlarmP3Threshold": 390
  }
}
```

Per-instance overrides in docker-compose via environment variables (e.g., `Sensor__SensorId=sensor-02`).

## Malicious Sensor Testing

To simulate a malicious/outlier sensor, override `Sensor__ValueMin` and `Sensor__ValueMax` to a range far outside normal (e.g., 750–900). After 2 consecutive consensus windows (~120 s), the sensor will be marked `BAD` by the ConsensusService.

Example (`docker-compose.malicious.yml`):
```yaml
services:
  sensor-simulator-04:
    environment:
      - Sensor__ValueMin=750
      - Sensor__ValueMax=900
```

```bash
docker compose -f docker-compose.yml -f docker-compose.malicious.yml up -d sensor-simulator-04
```

## Dependencies

- `api-gateway` (ingestion endpoint and auth)
