# NotificationService

Bridges Kafka events to browser clients via SignalR WebSockets. Applies priority-based dispatch: alarms are pushed immediately; normal readings are batched at 2 s intervals to avoid overwhelming the client.

## SignalR Hub

- Hub URL: `/ws/notifications`
- Auth: JWT via `Authorization: Bearer` header OR `?access_token=` query parameter (required for WebSocket connections which cannot set headers in browsers)
- Requires authenticated user (via `[Authorize]` on `NotificationHub`)

## Client-Side Events (Server → Client)

| Event | Trigger | Payload |
|-------|---------|---------|
| `SensorReading` | Batched every 2 s (normal readings) | `[{sensorId, value, timestamp, alarmPriority, quality}]` |
| `Alarm` | Immediate when `alarmPriority > 0` | `{sensorId, value, timestamp, alarmPriority}` |
| `StatusChanged` | Immediate on `sensor.status` event | `{sensorId, status, previousStatus, reason, timestamp}` |
| `QualityChanged` | Immediate on `consensus.result` event | `{sensorId, previousQuality, newQuality, timestamp}` |

## Dispatch Logic

```
sensor.data message received:
  alarmPriority > 0  →  Dispatcher.HandleSensorData → Alarm push (immediate, Hub.Clients.All)
  alarmPriority == 0 →  RateLimiter.Enqueue()        → SensorReading batch (every 2 s)

sensor.status message received → StatusChanged push (immediate)
consensus.result message received → QualityChanged push (immediate)
```

`RateLimiter` is a singleton BackgroundService that holds a `ConcurrentQueue<object>`. A `PeriodicTimer` (2 s) drains the queue and sends all accumulated readings as a single `SensorReading` array.

## CORS

Allowed origins: `http://localhost:4200`, `http://localhost:8080`. `AllowCredentials()` required for SignalR.

## Kafka Consumers

| Topic | Group | Handler |
|-------|-------|---------|
| `sensor.data` | `notification-sensordata` | `Dispatcher.HandleSensorData` |
| `sensor.status` | `notification-sensorstatus` | `Dispatcher.HandleStatusChange` |
| `consensus.result` | `notification-consensus` | `Dispatcher.HandleQualityChange` |

## Configuration

```json
{
  "Kafka": { "BootstrapServers": "kafka:9092" },
  "Jwt": { "Key": "...", "Issuer": "scada-auth-service", "Audience": "scada-platform" }
}
```

## Dependencies

- `kafka`

## WebSocket via Gateway

The YARP `ws-route` uses path `/ws/notifications/{**catch-all}` (the `{**catch-all}` catches the SignalR `/negotiate` sub-path). The gateway pipeline calls `app.UseWebSockets()` before YARP to enable WebSocket forwarding.
