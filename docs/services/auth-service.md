# AuthService

Handles user registration, login, JWT issuance, refresh token rotation, and sensor token provisioning.

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/login` | none | `{username, password}` → `{accessToken, refreshToken}` |
| POST | `/api/auth/refresh` | none | `{refreshToken}` → new pair; token reuse revokes whole session |
| GET | `/api/auth/verify` | Bearer | Returns 200 + claims if valid |
| POST | `/api/auth/sensor-token` | Bearer ADMIN | `{sensorId}` → long-lived JWT with `scope=ingest:write` |

## JWT Claims

```json
{ "sub": "<userId>", "role": "OPERATOR|ADMIN|SENSOR", "scope": "ingest:write", "exp": ..., "iat": ... }
```

Access token TTL: **15 min**. Sensor token TTL: **365 days**.

## Database — `postgres-auth`

```sql
CREATE TABLE users (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  username VARCHAR(64) UNIQUE NOT NULL,
  password_hash VARCHAR(256) NOT NULL,   -- bcrypt cost 11
  role VARCHAR(32) NOT NULL DEFAULT 'OPERATOR',
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Default seed: `admin / admin123` (ADMIN role), inserted on first startup if table is empty.

## Redis — `redis-auth`

```
refresh:{userId}:{tokenId}  →  {"issuedAt":..., "used":false}   TTL=7 days
```

Rotation: on `/refresh`, the old key is deleted and a new one is written. If the old key is already `used=true` (reuse detected), **all** refresh tokens for that user are deleted (full session revocation).

## Configuration

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=postgres-auth;Database=auth;Username=postgres;Password=postgres",
    "Redis": "redis-auth:6379"
  },
  "Jwt": {
    "Key": "<shared HMAC-SHA256 key, min 32 bytes>",
    "Issuer": "scada-auth-service",
    "Audience": "scada-platform"
  }
}
```

## Dependencies

- `postgres-auth` (PostgreSQL 16)
- `redis-auth` (Redis 7)
