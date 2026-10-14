# Conventions

Binding rules: spine AD-2 to AD-5, AD-11, AD-13 to AD-18, AD-23. This is the everyday cheat sheet. **Read it before creating new code** (CLAUDE.md and the BMad build/spec/review/dev skills load it).

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

## Files

Reference layout: the register feature.

```text
Auth.Api/Endpoints/
  RegisterEndpoint.cs       # route mapping + handler only
  RegisterRequest.cs        # one request type per file, DataAnnotations on it
  RegisterResponse.cs       # one response type per file
Auth.Application/Features/Register/
  RegisterCommand.cs        # RegisterCommand + RegisterResult may share this file
  RegisterUseCase.cs        # the use case, plus its Error definitions while under 100 lines
  RegisterErrors.cs         # only once RegisterUseCase.cs would reach 100 lines
```

- Each Api request and response type goes in its own file, named after the type, next to its endpoint. The endpoint file never declares contract types.
- A use case's file may also hold its `Error` definitions (`public static readonly Error …`) and small private helpers **while the file stays under 100 lines**. When it would reach 100, move the errors to `<Feature>Errors.cs` and each helper class to its own file in the same feature folder.
- A port interface goes in `Application/Abstractions/I<Name>.cs`. A small type that only exists for that port (e.g. `CommitOutcome` for `IUnitOfWork`) can share its file.

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
- Request-shape validation (required fields, formats, lengths) happens at the edge, using .NET built-ins only: DataAnnotations (`[Required]`, `[EmailAddress]`, `[StringLength]`, `[Url]`, …) on the request type, enabled once per service with `builder.Services.AddValidation()`. Hand-write a check only when no built-in attribute covers it. `MyCollection.ServiceDefaults.AddMyCollectionProblemDetails()` makes the automatic failure carry this project's `code: validation_failed`/`traceId` like every other error.
- Business rules (email already taken, limits, ownership, state transitions) live **only in Application**: the use case, or the Domain it calls. Endpoints and Infrastructure never decide a business rule. Infrastructure reports technical facts (e.g. `CommitOutcome.UniqueViolation`) and the use case decides what they mean.
- A request that uploads a file binds `[FromForm] TRequest` with an `IFormFile`/`IFormFileCollection` property, not JSON; call `.DisableAntiforgery()` on the route (Minimal APIs require it for form-binding endpoints if antiforgery services are ever registered). Make `TRequest` a plain class with `init`-only properties, **not** a positional record: the form complex-type binder requires every record constructor parameter to have a value present and throws an unhandled `BadHttpRequestException` otherwise — bypassing `[Required]`/`AddValidation()` entirely — even for a nullable parameter with a `= null` default. A settable-property class has no such constructor to satisfy; unset properties default to `null` and `[Required]` catches them normally.
- Routes are lowercase kebab-case. No `/v1` yet.

## Errors

- Use cases return `Result<T>` from `libs/MyCollection.Results`. Expected failures (wrong password, email taken) are typed `Error(Code, Kind, Message)`, never exceptions. Exceptions mean bugs.
- `ErrorKind` is fixed: `Validation`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `RateLimited`, `Unavailable`. ServiceDefaults maps each kind to an HTTP status; each service owns its own `code` values.
- Unique-constraint violations (Postgres `23505`) become `Conflict`, never a 500. `IUnitOfWork.CommitAsync` reports one as the neutral `CommitOutcome.UniqueViolation`, and the use case maps it to its own `Error`. Infrastructure never references a feature's errors. Use cases check the rule first (e.g. `EmailExistsAsync`) so the common case reads in the use case, and keep the commit outcome for the race.
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

## Passwords and emails

See [auth.md § Account linking](auth.md#account-linking) for the register rules: 8 to 32 character passwords, `EnhancedHashPassword`/`EnhancedVerify`, normalized emails.

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
