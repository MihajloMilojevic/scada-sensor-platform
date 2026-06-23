# SCADA Sensor Platform — Architecture Overview

## System Summary

A distributed SCADA (Supervisory Control and Data Acquisition) platform built on .NET 8 microservices. The system ingests signed, encrypted sensor readings, detects faults via Byzantine Fault-Tolerant consensus, manages a redundant sensor pool, and streams real-time alerts to a browser dashboard.

## Service Map

```
Browser (Angular 17)
     │  HTTP REST + WebSocket
     ▼
┌─────────────────┐
│   API Gateway   │  :8080 (external)
│   (YARP + JWT   │  Routes all traffic; caches JWT validation in Redis
│    cache)       │
└──┬──┬──┬──┬──┬──┘
   │  │  │  │  │
   │  │  │  │  └─▶ NotificationService  :8080  (SignalR hub)
   │  │  │  └────▶ AnalyticsService     :8080  (report aggregation)
   │  │  └───────▶ SensorMgmtService    :8080  (pool management)
   │  └──────────▶ IngestionService     :8080  (data ingestion)
   └─────────────▶ AuthService          :8080  (JWT issuance)

Kafka topics (async):
  sensor.data      ← IngestionService publishes
                   → ConsensusService, SensorMgmtService, NotificationService consume

  sensor.status    ← SensorMgmtService publishes
                   → NotificationService consumes

  consensus.result ← ConsensusService publishes
                   → SensorMgmtService, NotificationService consume

SensorSimulator × 5 (or 6):
  Signs + encrypts readings → POST /api/ingest (via Gateway)
```

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8 / ASP.NET Core |
| Message broker | Apache Kafka (KRaft, single broker) |
| Time-series store | InfluxDB 2.7 |
| Relational store | PostgreSQL 16 (one DB per service) |
| Cache / TTL | Redis 7 |
| API gateway | YARP ReverseProxy |
| Real-time push | ASP.NET Core SignalR |
| Auth | JWT Bearer (HMAC-SHA256, shared key) |
| Sensor security | ECDSA P-256 signature + AES-256-GCM encryption |
| Frontend | Angular 17+ standalone components |
| Container runtime | Docker Compose (dev), Kubernetes + Linkerd mTLS (prod) |

## Data Flow (happy path)

1. **SensorSimulator** generates a value, computes ECDSA signature over canonical JSON, encrypts with AES-256-GCM, POSTs to `POST /api/ingest` with a sensor JWT.
2. **IngestionService** validates JWT scope (`ingest:write`), decrypts, verifies signature, checks replay guard, writes to WAL, batches to InfluxDB, and publishes to `sensor.data` Kafka topic.
3. **ConsensusService** accumulates sensor readings in an A/B state store. Every 60 s it swaps stores, computes leave-one-out z-score outlier detection across GOOD sensors, and:
   - Writes a `consensus_results` row (mean value).
   - If a sensor is flagged outlier on 2 consecutive windows → marks it `BAD`, writes `quality_changes`, publishes to `consensus.result`.
4. **SensorManagementService** maintains a pool of exactly 5 ACTIVE sensors. It monitors heartbeats (Redis TTL 12 s) and promotes STANDBY sensors on failure. Consumes `consensus.result` to update quality flags.
5. **NotificationService** receives all three Kafka topics and dispatches via SignalR:
   - `alarmPriority > 0` → immediate `Alarm` push
   - normal readings → rate-limited 2 s batch `SensorReading` push
   - status/quality changes → immediate `StatusChanged` / `QualityChanged`
6. **AnalyticsService** composes data from Ingestion, Consensus, and SensorMgmt REST APIs with a 30 s PostgreSQL cache (`report_cache`).
7. **Angular frontend** connects to `/ws/notifications` via Gateway WebSocket, displays live sensor grid, alarm feed, and analytical reports.

## JWT Claims Contract

```json
{
  "sub":   "<userId | sensorId>",
  "role":  "OPERATOR | ADMIN | SENSOR",
  "scope": "ingest:write",
  "iat":   1234567890,
  "exp":   1234567890
}
```

- Access token TTL: 15 min
- Refresh token TTL: 7 days (Redis-backed, rotated on use)
- Sensor token TTL: 365 days
- `MapInboundClaims = false`; `RoleClaimType = "role"`, `NameClaimType = "sub"`

## Sensor Security Protocol

Every ingest message carries:

```json
{
  "sensorId": "sensor-01",
  "messageId": 4821,
  "timestamp": "2026-06-14T10:15:30.123Z",
  "value": 312.7,
  "alarmPriority": 0,
  "signature": "<base64 ECDSA-P256 over canonical JSON>",
  "encryptedPayload": "<base64 AES-256-GCM ciphertext>"
}
```

Validation order: JWT scope → decrypt → verify signature → replay guard (30 s window).

## Databases (per-service isolation)

| Service | DB | Port (host) | Purpose |
|---------|----|-------------|---------|
| AuthService | postgres-auth | 5435 | users, refresh token metadata |
| SensorMgmtService | postgres-sensormgmt | 5434 | sensors, status history |
| ConsensusService | postgres-consensus | 5433 | consensus_results, quality_changes |
| AnalyticsService | postgres-analytics | 5436 | report_cache |
| IngestionService | influxdb-ingestion | 8086 | sensor time-series |

## Redis Key Schemas

| Key pattern | Store | TTL | Value |
|-------------|-------|-----|-------|
| `token:{jwtHash}` | redis-gateway | remaining token lifetime | `{valid, claims, exp}` |
| `refresh:{userId}:{tokenId}` | redis-auth | 7 days | `{issuedAt, used}` |
| `heartbeat:{sensorId}` | redis-sensormgmt | 12 s | `"1"` |

## Alarm Priority Levels

| Priority | Meaning | Colour |
|----------|---------|--------|
| 0 | Normal | — |
| 1 | Warning | Yellow |
| 2 | High | Orange |
| 3 | Critical | Red |

Alarm thresholds per simulator: P1=350, P2=370, P3=390 (value units).
