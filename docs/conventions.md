# Conventions

Binding rules: spine AD-2 to AD-5, AD-11, AD-13 to AD-18. This is the everyday cheat sheet.

## Layout

```text
apps/<svc>/
  src/<Svc>.Domain/  <Svc>.Application/  <Svc>.Infrastructure/  <Svc>.Api/
  tests/<Svc>.UnitTests/  <Svc>.IntegrationTests/  <Svc>.ArchitectureTests/
  openapi/<svc>.json      # generated at build, committed
  Dockerfile              # targets: dev, migrate, final (build context = repo root)
libs/MyCollection.Results/           # Result<T>, Error, ErrorKind (no framework deps)
libs/MyCollection.ServiceDefaults/   # ASP.NET plumbing shared by every service
```

- Folder names: `-api` suffix for .NET HTTP services (`auth-api`, `wishlist-api`); `cards-bff`; `gateway`; `web`.
- Project names: `<Svc>.<Layer>` in PascalCase (`Auth.Api`).

## Layers

| If the code… | …it goes in |
|---|---|
| is an entity, value object or business rule with no I/O | `Domain` |
| orchestrates a use case, or declares an interface for I/O it needs (a *port*, incl. `IUnitOfWork`) | `Application` |
| talks to a DB, crypto, the clock, HTTP, OAuth or the filesystem (an *adapter*) | `Infrastructure` |
| maps HTTP ↔ use case, or wires DI in `Program.cs` | `Api` |

- References go inward only: `Api → Infrastructure → Application → Domain`.
- Endpoints never touch `DbContext`. Use cases never reference EF Core or ASP.NET.
- Application may reference only Domain and `MyCollection.Results`.
- No project references another service's projects. `libs/` never contains domain types, DTOs or service error codes.
- One use case = one transaction (`IUnitOfWork`). Time comes from `TimeProvider`, never `DateTime.UtcNow`.
- The acting user comes from `ICurrentUser.Id` only.
- Services never configure CORS; the gateway owns it.

## Endpoints

- Minimal APIs, one route group per feature, full public path: `app.MapGroup("/api/auth")`.
- Each endpoint: parse → call one use case → map the `Result` (3–5 lines).
- Request-shape validation (required fields, formats) happens at the edge. Business rules live in Application.
- Routes are lowercase kebab-case. No `/v1` yet.

## Errors

- Use cases return `Result<T>` from `libs/MyCollection.Results`. Expected failures (wrong password, email taken) are typed `Error(Code, Kind, Message)`, never exceptions. Exceptions mean bugs.
- `ErrorKind` is fixed: `Validation`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `RateLimited`, `Unavailable`. ServiceDefaults maps each kind to an HTTP status; each service owns its own `code` values.
- Unique-constraint violations (Postgres `23505`) become `Conflict`, never a 500.
- Every 4xx/5xx response is **RFC 9457 ProblemDetails**:
  ```json
  { "type": "...", "title": "Invalid credentials", "status": 401,
    "code": "invalid_credentials", "traceId": "00-…" }
  ```
- `code` is snake_case and stable once published.

## Data and JSON

- Ids: **UUIDv7**, created in code (`Guid.CreateVersion7()`).
- Time: UTC only (`timestamptz`, ISO-8601 with `Z`). The frontend converts to local time.
- JSON: camelCase, enums as strings.
- Database: snake_case, plural table names.
- Other services store a user reference as the JWT `sub` string.
- Emails are trimmed and lower-cased before storing or comparing.

## Packages

- Versions live **only** in `Directory.Packages.props`. A `.csproj` says `<PackageReference Include="X" />` with no version.
- `Directory.Build.props` sets `net10.0`, nullable and warnings-as-errors for all projects. `global.json` pins the SDK and selects the Microsoft Testing Platform test runner. `.config/dotnet-tools.json` pins `dotnet-ef`.

## Config and secrets

- Options are bound from env vars (`Section__Key`) and validated at startup. A missing value stops the app from booting.
- Compose reads `.env` (gitignored). Keep `.env.example` up to date when you add a variable.
- For IDE runs, `appsettings.Development.json` holds non-secret localhost defaults, and secrets go in `dotnet user-secrets`.
- Never commit secrets or keys (`*.pem`).

## Logging

- Serilog, structured JSON to **stdout** only. No log files.
- Every line carries `service`, `requestId` (from the gateway's `X-Request-Id`) and `traceId`.
- Never log tokens, passwords, secrets or full email addresses.

## Contract

- C# request/response records in `<Svc>.Api` are the source of truth. The build regenerates `openapi/<svc>.json`, which you commit. Review its diff in PRs; CI fails if it's stale.
- Startup side effects (key loading, config validation) are skipped when the OpenAPI generator runs the app.
- Clients generate types from it. Never hand-copy DTOs.
