# Stack and reference detail

> **Superseded where it differs** by the [architecture spine](../../planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md), which pins versions, layout and compose shape. This file keeps the reference detail carried over from the source notes.

The implementation shape comes from the source notes. Items marked *later* belong to future slices and are listed so the current slice leaves room for them.

## Services

| Service | Tech | Store | Status |
|---|---|---|---|
| Gateway | Nginx | — | now |
| Auth API | .NET 10 | PostgreSQL 18 | now |
| Wishlist Service | .NET | MongoDB 7 (schemaless because card shapes vary by game) | later |
| Cards BFF | .NET | Redis 7 cache (TTL 1h) + Scryfall, TCGPlayer, CardMarket | later |

Libraries: pinned in the spine's Stack table (`Microsoft.AspNetCore.Authentication.Google`, `AspNet.Security.OAuth.Discord`, JwtBearer, BCrypt.Net-Next).

## Auth API endpoints (behind the gateway at `/api`)

```
POST /auth/register
POST /auth/login
POST /auth/refresh-token
POST /auth/logout
GET  /auth/google            → redirect to Google
GET  /auth/google/callback
GET  /auth/discord           → redirect to Discord (added: Discord in scope)
GET  /auth/discord/callback
GET  /.well-known/jwks.json  → public signing keys (internal network)
```

Services serve the full `/api/auth/...` path themselves; the gateway doesn't rewrite paths (spine AD-14).

## PostgreSQL schema

```
users            id UUIDv7 PK, email UNIQUE, name, avatar_url, email_verified_at, created_at, updated_at
user_credentials id PK, user_id FK→users, password_hash (bcrypt)
user_oauth       id PK, user_id FK→users, provider (google|discord), provider_user_id, email, linked_at
refresh_tokens   id PK, user_id FK→users, family_id, token_hash (SHA-256), expires_at, consumed_at, revoked_at
```

Provider access tokens aren't stored (spine AD-10). Refresh-token behaviour is in AD-8.

## Later-slice interfaces (from notes)

Wishlist Service (per logged-in user): `GET /wishlist`, `POST /wishlist`, `DELETE /wishlist/:cardId`, and `PATCH /wishlist/:cardId` (priority or notes). It stores one Mongo document per user: `{ userId, cards: [{ cardId, cardName, addedAt, priority, notes }], updatedAt }`. Wishlist cards are associated with BFF price data.

Cards BFF: `GET /cards/search?name=`, `GET /cards/:cardId` (details plus price), `GET /cards/:cardId/prices`, and `GET /cards/wishlist-summary`.

## Gateway routing

```
/api/auth/*      → Auth API
/api/wishlist/*  → Wishlist Service   (later)
/api/cards/*     → Cards BFF          (later)
```

The gateway is also responsible for CORS, global rate limiting and request logging. JWT validation is an open question.

## Local dev

See the spine (AD-12, AD-19) and `docs/local-development.md`: `docker-compose.yml` (real topology) plus `docker-compose.override.yml` (dev: `dotnet watch`, direct ports auth-api 5001, Postgres 5432). Services: `gateway` (8080), `auth-db` (`postgres:18-alpine`, volume at `/var/lib/postgresql`), `auth-migrate` (one-shot EF bundle), `auth-api`. *Later:* `wishlist-api` (5002), `cards-bff` (5003), `mongo`, `redis`.

## Monorepo layout

Fixed in the spine's Structural Seed: `apps/{gateway,auth-api,...}`, `libs/MyCollection.ServiceDefaults`, `tools/`, `docs/`, with compose files at the root.

## Roadmap (source ordering, adjusted)

1. Docker Compose + Auth API (email/password, JWT)
2. Gateway (Nginx)
3. Social login (Google + Discord), moved forward into the first slice
4. Wishlist Service + auth integration
5. Cards BFF + Redis cache (parallel external calls, circuit breaker that falls back to stale cache, cache as rate-limit shield)
6. Observability (logs, health checks)
