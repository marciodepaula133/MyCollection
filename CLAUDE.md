# CLAUDE.md

MyCollection: an Nx monorepo of .NET 10 microservices behind an Nginx gateway, for trading-card collectors. It's also a portfolio piece, so correctness and clarity matter more than speed.

## Where to look

**Always start at [docs/README.md](docs/README.md).** It's the index: every doc is listed with the questions it answers. Open only what the task needs.

**Before creating or moving any code, read [docs/conventions.md](docs/conventions.md)**, especially § Files, § Endpoints and § Errors. This applies to plain Claude sessions and to every BMad skill.

| Need | File |
|---|---|
| Binding architecture rules (`AD-n`), pinned versions, deferred items | [ARCHITECTURE-SPINE.md](_bmad-output/planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md) |
| What the current slice must deliver (`CAP-n`) | [SPEC.md](_bmad-output/specs/auth/spec-mycollection-platform/SPEC.md) |
| Code placement, errors, naming, config, logging | [docs/conventions.md](docs/conventions.md) |
| Auth flows, tokens, cookies, linking rules | [docs/auth.md](docs/auth.md) |
| Running, debugging, ports, migrations | [docs/local-development.md](docs/local-development.md) |
| Why a decision was made | `.memlog.md` next to the spine |

Precedence: **spine > spec > docs**. `docs/history/` is historical and must not be treated as current.

## Rules that are easy to break

- Follow .NET / ASP.NET Core patterns (check what the framework or ASP.NET Core Identity does first); never borrow patterns from other ecosystems. Never add behaviour the spec doesn't ask for without raising it first.
- Hexagonal per service: `<Svc>.Domain ← Application ← Infrastructure ← Api`. No EF Core or ASP.NET types in Domain or Application; no `DbContext` in endpoints.
- Services never reference another service's projects or database. `libs/` holds plumbing only.
- NuGet versions only in `Directory.Packages.props`.
- Use cases return `Result<T>`; errors are ProblemDetails with a stable snake_case `code`.
- Request shape is validated with .NET DataAnnotations on the request type, not hand-written checks. Business rules (e.g. email taken) live only in Application, never in Infrastructure or the endpoint.
- Each Api request and response type gets its own file. A use case keeps its errors and helpers in its own file only while that file is under 100 lines; past that they move to `<Feature>Errors.cs` and helper files.
- The acting user comes only from the JWT `sub`. Refresh tokens only in the HttpOnly cookie.
- Migrations run only via the `<svc>-migrate` bundle, never `Database.Migrate()`.
- Never commit secrets, `.env` or `*.pem`. Never log tokens, passwords or secrets.
- After changing an API contract, commit the regenerated `apps/<svc>/openapi/<svc>.json`.

## Keeping docs current

When a change alters behaviour described in `docs/`, update that doc in the same change. When adding a doc, add its row to `docs/README.md`. When a change contradicts an `AD`, stop and raise it. Don't silently diverge; the spine is updated through `bmad-architecture` (update intent).

## Commands

Tooling is not scaffolded yet. See [docs/local-development.md](docs/local-development.md) for the planned commands (`docker compose up`, `nx affected -t build test`, `nx test <Project>`).

## Testing cadence

**Do not run any tests while implementing.** That includes "just the affected" test classes, filtered runs, single projects and running test executables directly. Tests are slow and burn tokens for no benefit mid-task.

- While working: **build only** (`dotnet build`) to catch compile errors. Write and update tests, but don't run them.
- Run the full suite (`dotnet test` / `nx affected -t test`) **once, right before opening a PR**. Run it earlier only when the user explicitly asks for that run.
- A spec's Verification section listing test commands does not override this. Those commands run at PR time.
