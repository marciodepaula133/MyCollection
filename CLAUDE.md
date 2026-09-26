# CLAUDE.md

MyCollection: an Nx monorepo of .NET 10 microservices behind an Nginx gateway, for trading-card collectors. It's also a portfolio piece, so correctness and clarity matter more than speed.

## Where to look

**Always start at [docs/README.md](docs/README.md).** It's the index: every doc is listed with the questions it answers. Open only what the task needs.

| Need | File |
|---|---|
| Binding architecture rules (`AD-n`), pinned versions, deferred items | [ARCHITECTURE-SPINE.md](_bmad-output/planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md) |
| What the current slice must deliver (`CAP-n`) | [SPEC.md](_bmad-output/specs/spec-mycollection-platform/SPEC.md) |
| Code placement, errors, naming, config, logging | [docs/conventions.md](docs/conventions.md) |
| Auth flows, tokens, cookies, linking rules | [docs/auth.md](docs/auth.md) |
| Running, debugging, ports, migrations | [docs/local-development.md](docs/local-development.md) |
| Why a decision was made | `.memlog.md` next to the spine |

Precedence: **spine > spec > docs**. `docs/history/` is historical and must not be treated as current.

## Rules that are easy to break

- Hexagonal per service: `<Svc>.Domain ← Application ← Infrastructure ← Api`. No EF Core or ASP.NET types in Domain or Application; no `DbContext` in endpoints.
- Services never reference another service's projects or database. `libs/` holds plumbing only.
- NuGet versions only in `Directory.Packages.props`.
- Use cases return `Result<T>`; errors are ProblemDetails with a stable snake_case `code`.
- The acting user comes only from the JWT `sub`. Refresh tokens only in the HttpOnly cookie.
- Migrations run only via the `<svc>-migrate` bundle, never `Database.Migrate()`.
- Never commit secrets, `.env` or `*.pem`. Never log tokens, passwords or secrets.
- After changing an API contract, commit the regenerated `apps/<svc>/openapi/<svc>.json`.

## Keeping docs current

When a change alters behaviour described in `docs/`, update that doc in the same change. When adding a doc, add its row to `docs/README.md`. When a change contradicts an `AD`, stop and raise it. Don't silently diverge; the spine is updated through `bmad-architecture` (update intent).

## Commands

Tooling is not scaffolded yet. See [docs/local-development.md](docs/local-development.md) for the planned commands (`docker compose up`, `nx affected -t build test`, `nx test <Project>`).
