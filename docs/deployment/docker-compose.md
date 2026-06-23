# Docker Compose Deployment

All services run together locally via `docker-compose.yml`.

## Quick Start

```bash
cd scada-sensor-platform

# Build and start everything
docker compose up -d --build

# Tail logs for a specific service
docker compose logs -f ingestion-service

# Stop all
docker compose down
```

## Service Ports (host)

| Container | Host port | Purpose |
|-----------|-----------|---------|
| api-gateway | **8080** | All external traffic |
| postgres-auth | 5435 | Auth DB (dev access) |
| postgres-sensormgmt | 5434 | SensorMgmt DB |
| postgres-consensus | 5433 | Consensus DB |
| postgres-analytics | 5436 | Analytics cache DB |
| influxdb-ingestion | 8086 | Time-series DB |
| redis-auth | 6381 | Auth refresh tokens |
| redis-sensormgmt | 6380 | Heartbeat TTLs |
| redis-gateway | 6382 | JWT validation cache |
| kafka | 9092 | Kafka broker |

All services internally communicate on port **8080**.

## Sensor Simulators

Five simulators run by default (`sensor-simulator-01` through `sensor-simulator-05`). Each is configured with:
- `Sensor__SensorId` — unique ID
- `Sensor__ValueMin` / `Sensor__ValueMax` — value range
- Individual ECDSA private key and sensor JWT (or auto-obtains via admin login)

## Malicious Sensor Override

```bash
# Override sensor-04 to send outlier values (750–900):
docker compose -f docker-compose.yml -f docker-compose.malicious.yml up -d sensor-simulator-04
```

`docker-compose.malicious.yml`:
```yaml
services:
  sensor-simulator-04:
    environment:
      - Sensor__ValueMin=750
      - Sensor__ValueMax=900
```

## Health Checks

All Postgres and Kafka containers have `healthcheck` blocks. Application services use `depends_on: condition: service_healthy` so they don't start until infrastructure is ready.

## Volumes

Named volumes provide data persistence:
- `postgres-auth-data`, `postgres-sensormgmt-data`, `postgres-consensus-data`, `postgres-analytics-data`
- `influx-data`
- `ingestion-wal` (mounted at `/data/wal` in ingestion-service)
- `kafka-data`

## Verifying the Stack

```bash
# Login
ACCESS=$(curl -s -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.accessToken')

# List sensors
curl -s http://localhost:8080/api/sensors -H "Authorization: Bearer $ACCESS" | jq '.[].sensor_id'

# Analytics summary
curl -s http://localhost:8080/api/reports/summary -H "Authorization: Bearer $ACCESS" | jq 'keys'

# Check heartbeat TTLs in Redis
docker exec redis-sensormgmt redis-cli KEYS "heartbeat:*"
```
