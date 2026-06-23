# AnalyticsService

Aggregates data from IngestionService, ConsensusService, and SensorManagementService into composed reports. Caches results in PostgreSQL with a configurable TTL to avoid repeatedly hitting upstream services.

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/reports/history` | Bearer | Params `sensorId, from, to` → raw sensor readings (from Ingestion) |
| GET | `/api/reports/consensus` | Bearer | Params `from, to` → consensus_results rows |
| GET | `/api/reports/quality-changes` | Bearer | Quality change history |
| GET | `/api/reports/sensors` | Bearer | All sensors with status and quality |
| GET | `/api/reports/summary` | Bearer | Combined: sensors + recent consensus + recent quality changes |
| GET | `/health` | none | Liveness check |

## Cache — `postgres-analytics`

```sql
CREATE TABLE report_cache (
  cache_key  VARCHAR(256) PRIMARY KEY,    -- first 32 hex chars of SHA256(endpoint+params)
  payload    JSONB NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at TIMESTAMPTZ NOT NULL
);
```

**Cache-aside pattern:**
1. Compute cache key = `SHA256(endpoint + query params)` truncated to 32 hex chars.
2. `SELECT payload FROM report_cache WHERE cache_key = ? AND expires_at > now()`
3. If hit → return cached payload (typically < 100 ms).
4. If miss → call upstream services → store result in `report_cache` with `expires_at = now() + TTL`.

Default TTL: **30 s** (configurable via `Cache:TtlSeconds`).

## Service-to-Service Authentication

AnalyticsService calls upstream APIs that require JWT. Rather than logging in via AuthService (adds coupling), it generates its own **service account JWT** using the shared `Jwt:Key`:

```
sub: "analytics-service"
role: "ADMIN"
exp: now + 1h
```

`ServiceTokenProvider` (singleton) caches the token and re-generates it 1 minute before expiry.

## Upstream Service Calls

`ReportComposer` (scoped) uses `HttpClient "upstream"` with a Bearer token from `ServiceTokenProvider`:

| Report | Upstream endpoint |
|--------|------------------|
| history | `GET /api/measurements?sensorId=...&from=...&to=...` → IngestionService |
| consensus | `GET /api/consensus?from=...&to=...` → ConsensusService |
| quality-changes | `GET /api/consensus/quality-changes` → ConsensusService |
| sensors | `GET /api/sensors` → SensorManagementService |
| summary | all four above, merged |

## Configuration

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=postgres-analytics;Database=analytics;Username=postgres;Password=postgres"
  },
  "Jwt": { "Key": "...", "Issuer": "scada-auth-service", "Audience": "scada-platform" },
  "Services": {
    "Ingestion":  "http://ingestion-service:8080",
    "Consensus":  "http://consensus-service:8080",
    "SensorMgmt": "http://sensormgmt-service:8080"
  },
  "Cache": { "TtlSeconds": "30" }
}
```

## Dependencies

- `postgres-analytics` (PostgreSQL 16)
- `ingestion-service`, `consensus-service`, `sensormgmt-service` (HTTP)
