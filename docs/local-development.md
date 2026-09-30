# Local development

Binding rules: spine AD-12, AD-17, AD-19.

> Status: the scaffold is in place — Nx workspace, the .NET solution, auth-api's four hexagonal projects, the gateway, the compose topology and CI all build and run as described below. No capability (CAP-1…CAP-8) is implemented yet: only `/health/live` and `/health/ready` respond for real. The routes in [services.md](services.md) are the target surface from the spec, not yet live.

## Prerequisites

- .NET SDK **10.0.401** (pinned in `global.json`)
- Node.js 24 LTS (only for Nx)
- Docker Desktop (compose, and Testcontainers for integration tests)
- Git and the GitHub CLI

## Three ways to run

| Mode | Command | Use it for | Reach it at |
|---|---|---|---|
| **Full stack** | `docker compose up` | Flows through the gateway, frontend work | `http://localhost:8080/api/...` |
| **One service in Docker** | `docker compose up auth-api` (also starts `auth-db` and `auth-migrate`) | Quick fixes, Postman | `http://localhost:5001/api/auth/...` |
| **One service in the IDE (debugger)** | `docker compose up auth-db auth-migrate`, then F5 in VS / VS Code (or `nx run Auth.Api:run`) | Breakpoints | `http://localhost:5001` |

- `docker compose up` automatically merges `docker-compose.override.yml` (dev: hot reload with `dotnet watch`, source mounted, direct ports).
- `docker compose -f docker-compose.yml up` runs the production-shaped images only.
- Hot reload uses a polling file watcher (needed for Docker on Windows), and `bin/`/`obj/` live in container volumes. It covers `.cs` changes. A new NuGet package or Dockerfile change needs `--build`. An env var change needs a restart.
- Scalar API page (dev only): `http://localhost:5001/scalar`. Postman can import `apps/auth-api/openapi/auth-api.json`.

## Ports (dev)

| Port | What |
|---|---|
| 8080 | gateway |
| 5001 | auth-api (direct) |
| 5432 | auth-db (PostgreSQL, for DB tools) |
| 5002 / 5003 | wishlist-api / cards-bff (later) |

## Secrets and config

- Copy `.env.example` to `.env` (gitignored) and fill in the Google/Discord client ids and secrets.
- Generate the dev JWT signing key with the script in `tools/`. The PEM is mounted into auth-api and never committed.
- IDE mode: the non-secret defaults are in `appsettings.Development.json`; set secrets with `dotnet user-secrets set "Google:ClientSecret" "..." --project apps/auth-api/src/Auth.Api`.
- OAuth redirect URIs to register with Google/Discord (dev), gateway and direct: `http://localhost:8080/api/auth/<provider>/callback` and `http://localhost:5001/api/auth/<provider>/callback`.
- Use Chrome, Edge or Firefox in dev. Safari drops `Secure` cookies over plain HTTP.

## Migrations

- Tools: `dotnet tool restore` installs the pinned `dotnet-ef`.
- Add one: `dotnet ef migrations add <Name> --project apps/auth-api/src/Auth.Infrastructure --startup-project apps/auth-api/src/Auth.Api`.
- Apply: migrations run only through the one-shot `auth-migrate` service (an EF migration bundle from the Dockerfile `migrate` target, connecting as the DB owner). After adding a migration, run `docker compose up --build auth-migrate`. The app never migrates itself; it connects as `auth_app` (data-only rights), and `/health/ready` stays unhealthy while migrations are pending.

## Logs

- `docker compose logs -f` shows everything, prefixed by service; `docker compose logs -f auth-api` shows one service.
- Follow one request across services: `docker compose logs | Select-String <requestId>`.
- Logs live in Docker and disappear when a container is recreated. A central log UI is planned for phase 6.

## Health

- `/health/live` and `/health/ready` exist on each service's internal network only. Compose uses them to order startup: `auth-db` healthy → `auth-migrate` done → `auth-api` ready → `gateway`.

## Gotchas found during scaffolding

- **`.dockerignore`'s `bin/`/`obj/` didn't work as written.** Unlike `.gitignore`, a bare `bin/`/`obj/` in `.dockerignore` only matches at the build-context root, not nested per-project folders — so the host's `apps/*/src/*/obj/project.assets.json` (baked with Windows-only NuGet paths) was leaking into the Linux build and breaking `dotnet publish` inside the container. Fixed with `**/bin/`/`**/obj/`. If a Docker build ever fails with a `FallbackPackagePathResolver`/NuGet path error, this is almost certainly why.
- **The gateway's health check used `wget --spider`, which sends `HEAD`.** The Minimal API health endpoints are `MapGet`-only and reject `HEAD` with `405`, so `auth-api` never went healthy under the original compose healthcheck. Fixed by dropping `--spider` for a real `GET` to `/dev/null`.
- **`proxy_pass` needs a variable, not a literal hostname, for AD-14's "resolves upstreams at request time" to actually hold.** `proxy_pass http://auth-api:8080;` makes nginx resolve `auth-api` once via the system resolver — outside compose (or before `auth-api` is reachable) `nginx -t`/startup fails hard with `host not found in upstream`. Fixed with `set $auth_upstream "..."; proxy_pass $auth_upstream;`, which defers resolution to the `resolver 127.0.0.11` directive at request time.
