# SCADA Sensor Platform

A distributed SCADA (Supervisory Control and Data Acquisition) system built on .NET 8 microservices. The platform ingests cryptographically-secured sensor data, detects malicious or faulty sensors via Byzantine Fault-Tolerant consensus, maintains a redundant sensor pool, and delivers real-time alerts to a browser dashboard.

## Architecture at a Glance

```
Sensor Simulators (×5)
        │  ECDSA-signed, AES-encrypted, JWT-authenticated POST /api/ingest
        ▼
  ┌─────────────┐      JWT cache (Redis)      ┌──────────────┐
  │ API Gateway │ ◄────────────────────────── │  AuthService │
  │ (YARP)      │                             └──────────────┘
  └──┬──┬──┬────┘
     │  │  │
     │  │  └──▶ NotificationService  ──▶  Browser (SignalR WebSocket)
     │  └─────▶ AnalyticsService     ◄── PostgreSQL report_cache
     └────────▶ SensorMgmtService
               ConsensusService
               IngestionService ──▶ InfluxDB + Kafka
```

**Kafka topics:**
- `sensor.data` — published by IngestionService; consumed by Consensus, SensorMgmt, Notification
- `sensor.status` — published by SensorMgmt; consumed by Notification
- `consensus.result` — published by Consensus; consumed by SensorMgmt, Notification

## Key Features

| Feature | Implementation |
|---------|---------------|
| Sensor authentication | JWT `scope=ingest:write` |
| Message integrity | ECDSA P-256 signature over canonical JSON |
| Message confidentiality | AES-256-GCM encryption |
| Replay protection | 30-second sliding-window `messageId` guard |
| Fault-tolerant ingestion | Write-Ahead Log with crash recovery |
| Outlier detection | Leave-one-out z-score (z > 2.0 threshold) |
| Pool redundancy | Exactly 5 ACTIVE sensors; STANDBY auto-promoted on failure |
| Real-time delivery | SignalR WebSocket, rate-limited (2 s batches for normal readings) |
| Alarm priority | P1 (warning) / P2 (high) / P3 (critical) — immediate push |
| Analytics caching | PostgreSQL cache-aside, SHA256 key, 30 s TTL |
| Service mesh | Linkerd mTLS in Kubernetes |

## Getting Started

### Prerequisites

- Docker + Docker Compose
- .NET 8 SDK (for local development / migrations)
- Node.js 22 + Angular CLI (for frontend development)

### Run with Docker Compose

```bash
git clone <repo>
cd scada-sensor-platform

docker compose up -d --build

# Wait ~30 s for all services to start, then:
ACCESS=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.accessToken')

curl -s http://localhost:8080/api/sensors -H "Authorization: Bearer $ACCESS" | jq '.[] | {id:.sensor_id, status:.status}'
```

Open the Angular frontend:
```bash
cd src/Frontend/scada-ui
npm install --legacy-peer-deps
ng serve
# Visit http://localhost:4200
```

### Run on Kubernetes (Minikube)

See [docs/deployment/kubernetes.md](docs/deployment/kubernetes.md).

## Documentation

| Document | Description |
|----------|-------------|
| [docs/architecture/README.md](docs/architecture/README.md) | Full system architecture, data flow, JWT contract, DB schemas |
| [docs/services/auth-service.md](docs/services/auth-service.md) | Authentication, JWT issuance, refresh token rotation |
| [docs/services/api-gateway.md](docs/services/api-gateway.md) | YARP routing, JWT cache, rate limiting |
| [docs/services/ingestion-service.md](docs/services/ingestion-service.md) | WAL, InfluxDB writes, Kafka publish, validation pipeline |
| [docs/services/sensormgmt-service.md](docs/services/sensormgmt-service.md) | Heartbeat monitor, pool manager, failover logic |
| [docs/services/consensus-service.md](docs/services/consensus-service.md) | BFT algorithm, leave-one-out z-score, quality downgrade |
| [docs/services/notification-service.md](docs/services/notification-service.md) | SignalR hub, dispatch logic, rate limiter |
| [docs/services/analytics-service.md](docs/services/analytics-service.md) | Report composition, cache-aside pattern, service token |
| [docs/services/sensor-simulator.md](docs/services/sensor-simulator.md) | Crypto, alarm thresholds, malicious sensor testing |
| [docs/services/frontend.md](docs/services/frontend.md) | Angular routes, services, SignalR integration |
| [docs/services/shared-libraries.md](docs/services/shared-libraries.md) | Shared.Contracts, Shared.Kafka, Shared.Security |
| [docs/deployment/docker-compose.md](docs/deployment/docker-compose.md) | Local Docker Compose setup and verification |
| [docs/deployment/kubernetes.md](docs/deployment/kubernetes.md) | Minikube + Linkerd setup, known limitations, smoke test |

## Service Ports (Docker Compose)

| Service | External port |
|---------|--------------|
| API Gateway | **8080** |
| InfluxDB | 8086 |
| Postgres (auth) | 5435 |
| Postgres (sensormgmt) | 5434 |
| Postgres (consensus) | 5433 |
| Postgres (analytics) | 5436 |
| Kafka | 9092 |

## Project Structure

```
scada-sensor-platform/
├── docker-compose.yml
├── docker-compose.malicious.yml      (override for malicious sensor testing)
├── k8s/
│   ├── base/                         (Kustomize base manifests)
│   └── overlays/minikube/            (Minikube-specific patches)
├── src/
│   ├── ApiGateway/
│   ├── AuthService/
│   ├── IngestionService/
│   ├── ConsensusService/
│   ├── SensorManagementService/
│   ├── NotificationService/
│   ├── AnalyticsService/
│   ├── SensorSimulator/
│   ├── Frontend/scada-ui/            (Angular 17+)
│   └── Shared/
│       ├── Scada.Shared.Contracts/
│       ├── Scada.Shared.Security/
│       └── Scada.Shared.Kafka/
└── docs/
    ├── architecture/
    ├── services/
    └── deployment/
```

## Default Credentials

| Resource | Username | Password |
|----------|----------|---------|
| Admin user | `admin` | `admin123` |
| PostgreSQL | `postgres` | `postgres` |
| InfluxDB | `admin` | `adminpassword` |

> **Note:** These are development defaults. Do not use in production.
