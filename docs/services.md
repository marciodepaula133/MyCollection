# Services catalog

| Service | Folder | Public route (via gateway `:8080`) | Direct dev port | Datastore | Status |
|---|---|---|---|---|---|
| gateway | `apps/gateway` | `/` (entry point) | 8080 | none | Slice 1 |
| auth-api | `apps/auth-api` | `/api/auth/*` | 5001 | PostgreSQL 18 (`auth-db`, dev port 5432) | Slice 1 |
| auth-migrate | (compose service, built from auth-api) | none | none | runs migrations on `auth-db`, then exits | Slice 1 |
| wishlist-api | `apps/wishlist-api` | `/api/wishlist/*` | 5002 | MongoDB | Later |
| cards-bff | `apps/cards-bff` | `/api/cards/*` | 5003 | Redis (cache) + external APIs | Later |
| web | `apps/web` | n/a (frontend) | n/a | none | Later |

Direct ports and the Postgres port exist only in `docker-compose.override.yml` (dev). Outside dev, only the gateway is published.

## auth-api

Routes, all under `/api/auth` unless noted:

| Method | Path | Purpose | CAP |
|---|---|---|---|
| POST | `/register` | Create a password account | CAP-1 |
| POST | `/login` | Password login → access token (body) + refresh cookie | CAP-2 |
| POST | `/refresh-token` | Rotate the refresh cookie → new access token | CAP-3 |
| POST | `/logout` | Revoke the refresh-token family | CAP-4 |
| GET | `/google`, `/google/callback`, `/google/complete` | Google sign-in (callback is owned by the ASP.NET handler) | CAP-5 |
| GET | `/discord`, `/discord/callback`, `/discord/complete` | Discord sign-in | CAP-6 |
| GET | `/.well-known/jwks.json`, `/.well-known/openid-configuration` (root path, internal) | Public signing keys and discovery for other services | AD-6 |
| GET | `/health/live`, `/health/ready` (internal) | Health for Docker | AD-19 |

Tables: `users`, `user_credentials`, `user_oauth`, `refresh_tokens`. See [auth.md](auth.md).

Contract: `apps/auth-api/openapi/auth-api.json` (generated, committed). In dev, the interactive API page is Scalar at `http://localhost:5001/scalar`.

## gateway

Nginx (`nginx:1.30-alpine`), config rendered from `templates/` with env vars. Responsibilities:

- prefix routing (no path rewriting; upstreams resolved per request, so it survives service restarts);
- CORS from `GATEWAY_CORS_ORIGINS` (it answers preflights itself; headers are added even on errors; unlisted origins get 403). Services never configure CORS;
- rate limits: 10 r/s per IP with burst 20; login and register 5/min per IP with burst 5; `429` when exceeded;
- `X-Request-Id` (always its own) and `X-Forwarded-*` (client values overwritten; host keeps the port);
- JSON access log, and `application/problem+json` bodies for its own errors.

It does **not** validate JWTs and does **not** expose services' `/health/*` or `/.well-known/*`.
