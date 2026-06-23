# ConsensusService

Implements a Byzantine Fault-Tolerant consensus algorithm over the sensor pool. Runs a 60-second window cycle that detects outlier/malicious sensors and downgrades their quality.

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/consensus` | Bearer | Query params `from, to` → `consensus_results` rows |
| GET | `/api/consensus/quality-changes` | Bearer | `quality_changes` history |
| GET | `/health` | none | Liveness check |

## Database — `postgres-consensus`

```sql
CREATE TABLE consensus_results (
  id                  BIGSERIAL PRIMARY KEY,
  window_start        TIMESTAMPTZ NOT NULL,
  window_end          TIMESTAMPTZ NOT NULL,
  consensus_value     DOUBLE PRECISION NOT NULL,   -- mean of GOOD sensors
  contributing_sensors INT NOT NULL,               -- count of GOOD sensors included
  created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE quality_changes (
  id               BIGSERIAL PRIMARY KEY,
  sensor_id        VARCHAR(64) NOT NULL,
  previous_quality VARCHAR(16) NOT NULL,
  new_quality      VARCHAR(16) NOT NULL,
  sensor_value     DOUBLE PRECISION NOT NULL,
  consensus_value  DOUBLE PRECISION NOT NULL,
  deviation_sigma  DOUBLE PRECISION NOT NULL,
  changed_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

## BFT Algorithm

### State Store (A/B double-buffer)

`StateStoreManager` holds two `ConcurrentDictionary<string, ConcurrentBag<double>>` stores (A and B). The active store collects incoming readings; every 60 s `SwapAndSnapshot()` atomically swaps the `volatile` active reference and returns the full snapshot of the now-inactive store.

```csharp
var snapshot = stateManager.SwapAndSnapshot();  // lock-free atomic swap
await engine.ProcessWindowAsync(snapshot, windowStart, windowEnd);
```

### Outlier Detection — Leave-One-Out Z-Score

For each GOOD sensor, compute the mean `μ` and population σ of **all other** GOOD sensors, then:

```
z = |sensorMean - μ_others| / σ_others
```

If `z > 2.0` → sensor is an outlier for this window.

> **Why leave-one-out?** Population z-score for 1 outlier among N sensors is mathematically bounded at `sqrt((N-1)²/N)`. For N=5 this equals exactly 2.0, making `> 2.0` unreachable. Leave-one-out gives z ≈ 45 for a clearly malicious sensor (e.g., value 800 vs normal 300), making detection reliable.

### Quality Downgrade Rule

- Sensor flagged outlier for **2 consecutive windows** → quality set to `BAD`
- `quality_changes` row written, `consensus.result` Kafka event published
- A sensor recovering to normal resets its consecutive-outlier counter on the next clean window

### Consensus Value

`consensus_value` = mean of **all GOOD-quality sensors**' per-window averages. BAD sensors are excluded from the mean after downgrade.

## Kafka

### Consumes

| Topic | Group | Action |
|-------|-------|--------|
| `sensor.data` | `consensus-sensordata` | Feed `stateManager.AddValue(sensorId, value)` |

### Publishes

Topic: `consensus.result` (only on quality change)

```json
{
  "sensorId": "sensor-04",
  "previousQuality": "GOOD",
  "newQuality": "BAD",
  "consensusValue": 305.2,
  "sensorValue": 820.4,
  "deviationSigma": 45.3,
  "timestamp": "2026-06-14T10:21:00.000Z"
}
```

## Configuration

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=postgres-consensus;Database=consensus;Username=postgres;Password=postgres"
  },
  "Kafka": { "BootstrapServers": "kafka:9092" },
  "Jwt": { "Key": "...", "Issuer": "scada-auth-service", "Audience": "scada-platform" }
}
```

## Dependencies

- `postgres-consensus` (PostgreSQL 16)
- `kafka`
