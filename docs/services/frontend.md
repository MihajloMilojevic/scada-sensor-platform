# Frontend (Angular SCADA UI)

Angular 17+ SPA with standalone components, lazy-loaded routes, and real-time SignalR integration.

## Routes

| Path | Component | Description |
|------|-----------|-------------|
| `/login` | `LoginComponent` | Username/password form |
| `/dashboard` | `DashboardComponent` | Live sensor grid + recent events feed |
| `/alarms` | `AlarmsComponent` | Alarm log, status changes, quality changes |
| `/sensors` | `SensorsComponent` | Sensor table with activate/deactivate/block buttons |
| `/history` | `HistoryComponent` | Date range picker → raw reading table |
| `/reports` | `ReportsComponent` | Analytics summary: sensors + consensus + quality changes |

All routes except `/login` require authentication (`authGuard`). Redirects to `/dashboard` on root.

## Services

### `AuthService`
- `login(username, password)` → `POST /api/auth/login`, stores `accessToken` + `refreshToken` in `localStorage`
- `logout()` → clears storage, navigates to `/login`
- `getToken()` → returns current access token
- `getAuthHeaders()` → `{Authorization: "Bearer <token>"}`
- `isLoggedIn` → Angular signal (reactive)

### `NotificationService`
- Creates a `HubConnection` to `http://localhost:8080/ws/notifications`
- `accessTokenFactory: () => auth.getToken()` (SignalR sends this as `?access_token=` for WS)
- `withAutomaticReconnect()` for resilience
- `connect()` → starts connection and registers handlers
- Reactive signals: `sensorReadings()`, `alarms()`, `statusEvents()`, `qualityEvents()`, `connected()`

### `SensorApiService`
- `getSensors()` → `GET /api/sensors`
- `activate(id)` / `deactivate(id)` / `block(id)` → respective POST endpoints

### `AnalyticsApiService`
- `getSummary()` → `GET /api/reports/summary`
- `getHistory(from, to)` → `GET /api/reports/history`
- `getConsensus(from, to)` → `GET /api/reports/consensus`
- `getQualityChanges()` → `GET /api/reports/quality-changes`
- `getSensorReport()` → `GET /api/reports/sensors`

## Dashboard — Sensor Grid

Cards colour-coded by `alarmPriority`:
- `p1` → yellow border/background
- `p2` → orange border/background
- `p3` → red border/background + CSS pulse animation

Shows current `value`, `quality` (green=GOOD, red=BAD), last `timestamp`, and alarm badge.

## SignalR Event Handling

```typescript
// DashboardComponent.ngOnInit
ns.connect();

// registered in NotificationService.connect():
conn.on('SensorReading', (batch: SensorReading[]) => ...)  // update sensorReadings signal
conn.on('Alarm', (alarm) => ...)                           // push to alarms signal
conn.on('StatusChanged', (ev) => ...)                      // push to statusEvents signal
conn.on('QualityChanged', (ev) => ...)                     // push to qualityEvents signal
```

## Build

```bash
cd src/Frontend/scada-ui
npm install --legacy-peer-deps
ng build                        # development build → dist/
ng build --configuration production  # production build
```

Production output: `dist/scada-ui/browser/` (served by nginx in Docker).

## Docker

`Dockerfile` uses multi-stage build:
1. `node:22-alpine` → `npm install` + `ng build`
2. `nginx:alpine` → copies `dist/scada-ui/browser/` + custom `nginx.conf` (SPA fallback to `index.html`)

Port: **80**

## Configuration

The API base URL is hardcoded to `http://localhost:8080` in services (dev). For production/K8s, update `GW` constant in each service file or use Angular environment files.
