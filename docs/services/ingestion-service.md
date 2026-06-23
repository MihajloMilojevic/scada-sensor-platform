# IngestionService

Receives encrypted, signed sensor readings; validates them; durably buffers them via a Write-Ahead Log (WAL); batch-writes to InfluxDB; and publishes to Kafka.

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/ingest` | Bearer `scope=ingest:write` | Accept a sensor reading; returns 202 after WAL write |
| GET | `/api/measurements` | Bearer | Query params `sensorId, from, to` → array of readings from InfluxDB |
| GET | `/health` | none | Liveness check |

## Request Format (`IngestRequest`)

```json
{
  "sensorId": "sensor-01",
  "messageId": 4821,
  "timestamp": "2026-06-14T10:15:30.123Z",
  "value": 312.7,
  "alarmPriority": 0,
  "signature": "<base64 ECDSA-P256 over canonical JSON of above fields>",
  "encryptedPayload": "<base64 AES-256-GCM ciphertext of above fields>"
}
```

## Validation Pipeline

1. **JWT**: must have `scope=ingest:write` claim.
2. **Decrypt**: AES-256-GCM with pre-shared key.
3. **Verify signature**: ECDSA P-256 over canonical (sorted-key) JSON of `{sensorId, messageId, timestamp, value, alarmPriority}`.
4. **Replay guard**: rejects if `messageId` was seen in the last 30 s for the same sensor.

All rejections before step 4 return `401`; replay/stale timestamp returns `400`.

## Write-Ahead Log (WAL)

- Path: `/data/wal/{sensorId}.jsonl` (one JSONL file per sensor)
- Written synchronously before returning 202 — crash safety
- **FlushWorker** (BackgroundService) flushes batches every 5 s or when 100 entries accumulate
- On flush: writes to InfluxDB + publishes to Kafka → then clears WAL file
- On startup: **WAL recovery** re-reads any unflushed files and re-processes them

## InfluxDB Schema

- Bucket: `sensor-data`
- Measurement: `temperature`
- Tags: `sensorId`
- Fields: `value` (float), `alarmPriority` (int), `quality` (string)

## Kafka

- Topic: `sensor.data`
- Message key: `sensorId` (ordering per sensor)

```json
{
  "sensorId": "sensor-01",
  "value": 312.7,
  "timestamp": "2026-06-14T10:15:30.123Z",
  "alarmPriority": 0,
  "quality": "GOOD"
}
```

## Configuration

```json
{
  "InfluxDb": {
    "Url": "http://influxdb-ingestion:8086",
    "Token": "scada-influx-token",
    "Org": "scada",
    "Bucket": "sensor-data"
  },
  "Kafka": { "BootstrapServers": "kafka:9092" },
  "Jwt": { "Key": "...", "Issuer": "scada-auth-service", "Audience": "scada-platform" },
  "Security": { "EcdsaPublicKey": "<base64 PKCS8>" }
}
```

## Dependencies

- `kafka` (Kafka broker)
- `influxdb-ingestion` (InfluxDB 2.7)
