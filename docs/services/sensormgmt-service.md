# SensorManagementService

Maintains a pool of exactly 5 ACTIVE sensors via heartbeat monitoring and automatic failover. Exposes a REST API for manual sensor control.

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/sensors` | Bearer | List all sensors with status and quality |
| GET | `/api/sensors/{id}` | Bearer | Single sensor detail |
| POST | `/api/sensors/{id}/activate` | Bearer ADMIN | Manual activation |
| POST | `/api/sensors/{id}/deactivate` | Bearer ADMIN | Manual deactivation |
| POST | `/api/sensors/{id}/block` | Bearer ADMIN | Temporarily mark INACTIVE (simulates failure for testing) |
| GET | `/health` | none | Liveness check |

## Database — `postgres-sensormgmt`

```sql
CREATE TABLE sensors (
  sensor_id    VARCHAR(64) PRIMARY KEY,
  status       VARCHAR(16) NOT NULL,       -- ACTIVE, INACTIVE, STANDBY
  quality      VARCHAR(16) NOT NULL DEFAULT 'GOOD',  -- GOOD, BAD, UNCERTAIN
  value_min    DOUBLE PRECISION,
  value_max    DOUBLE PRECISION,
  alarm_thresholds JSONB,                  -- {"p1":350,"p2":370,"p3":390}
  last_seen_at TIMESTAMPTZ,
  updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE sensor_status_history (
  id         BIGSERIAL PRIMARY KEY,
  sensor_id  VARCHAR(64) NOT NULL,
  old_status VARCHAR(16),
  new_status VARCHAR(16) NOT NULL,
  reason     VARCHAR(32) NOT NULL,         -- AUTO_FAILOVER, MANUAL, CONSENSUS_QUALITY_CHANGE
  changed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Seeded on first startup: `sensor-01` through `sensor-05` → ACTIVE; `sensor-06` → STANDBY.

## Redis Heartbeat — `redis-sensormgmt`

```
heartbeat:{sensorId}  →  "1"   TTL=12 s
```

`SensorDataConsumer` (Kafka) refreshes the key on every `sensor.data` message. Sensors send every 1–5 s, so TTL=12 s gives ~2–3 missed readings before expiry.

## Fault Tolerance Logic

**HeartbeatMonitor** (BackgroundService, polls every 1 s):
1. For each ACTIVE sensor, `EXISTS heartbeat:{sensorId}`.
2. If key is missing **and** `last_seen_at IS NOT NULL` (guard against false-positive on fresh seed): mark sensor INACTIVE, write history, publish `sensor.status`.
3. Count ACTIVE sensors; if `< 5`, pick the first STANDBY sensor (by sensor_id alphabetical order), set to ACTIVE, publish `sensor.status` with `reason=AUTO_FAILOVER`.

SemaphoreSlim prevents concurrent failover races (only one failover at a time).

## Kafka

### Consumes

| Topic | Group | Action |
|-------|-------|--------|
| `sensor.data` | `sensormgmt-sensordata` | Refresh heartbeat TTL, update `last_seen_at` |
| `consensus.result` | `sensormgmt-consensus` | Update `sensors.quality = newQuality` |

### Publishes

Topic: `sensor.status`

```json
{
  "sensorId": "sensor-06",
  "status": "ACTIVE",
  "previousStatus": "STANDBY",
  "reason": "AUTO_FAILOVER",
  "timestamp": "2026-06-14T10:20:00.000Z"
}
```

## Configuration

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=postgres-sensormgmt;Database=sensormgmt;Username=postgres;Password=postgres",
    "Redis": "redis-sensormgmt:6379"
  },
  "Kafka": { "BootstrapServers": "kafka:9092" },
  "Jwt": { "Key": "...", "Issuer": "scada-auth-service", "Audience": "scada-platform" }
}
```

## Dependencies

- `postgres-sensormgmt` (PostgreSQL 16)
- `redis-sensormgmt` (Redis 7)
- `kafka`
