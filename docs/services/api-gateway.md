# ApiGateway

Single external entry point. Validates JWTs, caches validation results in Redis, enforces rate limits, and proxies all traffic via YARP.

## Routing

| Route | Path pattern | Backend |
|-------|-------------|---------|
| auth-route | `/api/auth/{**catch-all}` | `auth-service:8080` |
| ingest-route | `/api/ingest/{**catch-all}` | `ingestion-service:8080` |
| sensors-route | `/api/sensors/{**catch-all}` | `sensormgmt-service:8080` |
| reports-route | `/api/reports/{**catch-all}` | `analytics-service:8080` |
| ws-route | `/ws/notifications/{**catch-all}` | `notification-service:8080` |

The `{**catch-all}` suffix on `ws-route` is required to capture both the `/negotiate` HTTP handshake and the WebSocket upgrade.

## Middleware Pipeline

```
UseWebSockets()
  → JwtCacheMiddleware       (validates Bearer token, caches result in Redis)
  → RateLimiter              (10 req/s per JWT sub, keyed on sub claim)
  → MapReverseProxy()
```

`JwtCacheMiddleware` also reads `?access_token=` query parameter — required for SignalR WebSocket connections which cannot set HTTP headers.

## JWT Validation Cache — `redis-gateway`

```
token:{sha256(jwtString)}  →  {valid, sub, role, exp}   TTL = remaining token lifetime
```

Cache miss: calls `GET http://auth-service:8080/api/auth/verify`. Cache hit: skips auth-service entirely.

## Rate Limiting

- 10 requests/second per authenticated `sub` claim (sliding window)
- Login and refresh endpoints bypass rate limiting (no JWT on those calls)
- Returns `HTTP 429` with `{"error":"Too many requests"}` on excess

## Configuration

```json
{
  "ConnectionStrings": { "Redis": "redis-gateway:6379" },
  "AuthService": { "BaseUrl": "http://auth-service:8080" },
  "Jwt": { "Key": "...", "Issuer": "scada-auth-service", "Audience": "scada-platform" },
  "RateLimit": { "PermitLimit": "10", "WindowSeconds": "1" }
}
```

## Dependencies

- `auth-service` (for cache-miss JWT validation)
- `redis-gateway` (Redis 7, JWT validation cache)
- All backend services (as YARP destinations)
