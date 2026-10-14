---
title: 'CAP-1: Register with email and password'
type: 'feature'
created: '2026-10-02'
status: 'done'
baseline_commit: '95018f41c6d933897d282216f2265c911e9e9b95'
route: 'dispatch'
review_loop_iteration: 0
context: ['{project-root}/_bmad-output/planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `auth-api` is still an empty hexagonal scaffold (setup slice only) — no `User` entity, no persistence, no endpoint. A visitor has no way to create an account.

**Approach:** Implement the `Register` use case end-to-end: a `/api/auth/register` Minimal API endpoint that parses the request, calls an Application use case, which normalizes the email, hashes the password with bcrypt, and persists a new `User` via a repository port. A duplicate email yields a `Conflict` result (409, `code: email_already_registered`), not a 500.

## Boundaries & Constraints

**Always:**
- Follow AD-2/AD-11/AD-13 layering: `Auth.Domain` holds `User` with no I/O; `Auth.Application` declares `IUserRepository`, `IPasswordHasher`, `IUnitOfWork` and references only Domain + `MyCollection.Results`; `Auth.Infrastructure` implements the ports (EF Core, BCrypt.Net-Next) and owns the EF Core migration; `Auth.Api`'s endpoint only parses → calls the use case → maps `Result<T>`.
- Emails are trimmed and lower-cased before compare/store; `users.email` is unique; `users.email_verified_at` stays null for password sign-ups (AD-10).
- Passwords are bcrypt-hashed before persistence; the plaintext password is never logged or stored.
- Ids are `Guid.CreateVersion7()`; timestamps come from the injected `TimeProvider` (UTC), never `DateTime.UtcNow`.
- The use case owns the duplicate-email rule: it checks `IUserRepository.EmailExistsAsync` before hashing, and maps a neutral `CommitOutcome.UniqueViolation` from `IUnitOfWork.CommitAsync` (the race where two requests both pass the check) to `Error("email_already_registered", ErrorKind.Conflict, ...)`. Infrastructure only reports that Postgres `23505` happened; it never references a feature or its errors. Never surfaced as a 500 (renegotiated 2026-10-10).
- The EF Core migration lives in `Auth.Infrastructure`; it is applied only by the `auth-migrate` bundle, never `Database.Migrate()`.
- `apps/auth-api/openapi/auth-api.json` is regenerated and committed once the endpoint exists.
- Remove `.WithoutRequiringPositiveResults()` from the `Auth.ArchitectureTests` layering rules that now have real classes to evaluate (per `docs/testing.md`'s status note).
- Password length is 8 to 32 characters inclusive, no composition rules (minimum decided 2026-10-02, maximum decided 2026-10-07).
- Passwords are hashed with BCrypt.Net-Next's `EnhancedHashPassword` (SHA-384 pre-hash) and verified later with `EnhancedVerify`; never plain `HashPassword` (decided 2026-10-07).
- This slice adds `apps/auth-api/db/init.sql`, which creates the DML-only `auth_app` role and its grants (including `ALTER DEFAULT PRIVILEGES` for tables the migration bundle creates later), mounted into `auth-db` so register works under `docker compose up` (decided 2026-10-07).
- This slice adds `docs/database.md` (roles and superusers, how to connect, local DB commands, data persistence), indexed in `docs/README.md` (decided 2026-10-07).

**Never:**
- No token issuance here — login (CAP-2) is a separate use case; register does not return an access or refresh token.
- No email verification or password reset (deferred to `spec-email-identity`).
- No rate limiting in `auth-api` — that's the gateway's job (CAP-7, AD-14).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | Well-formed email + password, no existing user with that email | `201 Created`, body `{ id, email }` (no password/hash echoed); row inserted into `users` and `user_credentials` with a bcrypt hash | N/A |
| Duplicate email | Email (any casing/whitespace) already registered | `409` ProblemDetails, `code: email_already_registered` | Caught by the use case's existence check; a concurrent duplicate that slips past it hits the unique index, comes back as `CommitOutcome.UniqueViolation`, and the use case maps it to the same `Conflict`. Never a 500 |
| Malformed email | e.g. `"not-an-email"` | `400` ProblemDetails, `code: validation_failed` | Rejected at the endpoint edge, before the use case runs |
| Email with whitespace/mixed case | `" Foo@Bar.com "` | Normalized to `foo@bar.com` before uniqueness check and storage | N/A |
| Password shorter than 8 characters | e.g. `"short"` | `400` ProblemDetails, `code: validation_failed` | Rejected at the endpoint edge |
| Password longer than 32 characters | e.g. 33 characters | `400` ProblemDetails, `code: validation_failed` | Rejected at the endpoint edge |
| Missing/null `email` or `password` field | request body omits the field | `400` ProblemDetails, `code: validation_failed` | Minimal API model binding / edge validation rejects before the use case runs |

</frozen-after-approval>

## Code Map

- `apps/auth-api/db/init.sql` -- new; runs once as `auth_owner` on an empty data volume. Creates the app role and grants `CONNECT`, `USAGE` on `public`, and default `SELECT/INSERT/UPDATE/DELETE` on future tables.
- `docker-compose.yml` (`auth-db`) -- mount `init.sql` into `/docker-entrypoint-initdb.d/`; pass `AUTH_APP_DB_USER` and `AUTH_APP_DB_PASSWORD` (already in `.env.example`, unused today).
- `docs/database.md` -- new; `docs/README.md` -- add its row; `docs/local-development.md` -- link to it from the migrations section.

- `apps/auth-api/src/Auth.Domain/User.cs` -- new entity: `Id` (UUIDv7), `Email`, `Name?`, `AvatarUrl?`, `EmailVerifiedAt?`, `CreatedAt`, `UpdatedAt`.
- `apps/auth-api/src/Auth.Application/Abstractions/IUserRepository.cs`, `IPasswordHasher.cs`, `IUnitOfWork.cs` -- new ports; Application has nothing but `Auth.Domain` + `libs/MyCollection.Results` today.
- `apps/auth-api/src/Auth.Application/Features/Register/RegisterCommand.cs`, `RegisterUseCase.cs` -- new; normalizes email, hashes password, builds `User`, persists via `IUserRepository`, commits via `IUnitOfWork`, returns `Result<RegisterResult>`.
- `apps/auth-api/src/Auth.Infrastructure/AuthDbContext.cs` -- currently an empty `DbContext`; add `DbSet<User>` and the `user_credentials` mapping.
- `apps/auth-api/src/Auth.Infrastructure/Entities/` -- new EF entity configurations for `users` (snake_case, plural) and `user_credentials` (per `stack.md` schema).
- `apps/auth-api/src/Auth.Infrastructure/Repositories/UserRepository.cs` -- new; implements `IUserRepository`, translates Postgres `23505` to `Conflict`.
- `apps/auth-api/src/Auth.Infrastructure/Security/BcryptPasswordHasher.cs` -- new; implements `IPasswordHasher` using the already-pinned `BCrypt.Net-Next`'s `EnhancedHashPassword`/`EnhancedVerify` (add `PackageReference` with no version, per `Directory.Packages.props`; `Auth.Infrastructure.csproj` also needs `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` for the ready check).
- `apps/auth-api/src/Auth.Infrastructure/AuthDbContextFactory.cs` -- exists, reads `ConnectionStrings__Postgres`; do not change.
- `libs/MyCollection.ServiceDefaults/MyCollection.ServiceDefaults.csproj` -- references only `MyCollection.Results`; needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for the ProblemDetails mapping.
- `apps/auth-api/src/Auth.Infrastructure/UnitOfWork.cs` -- new; wraps `AuthDbContext.SaveChangesAsync`.
- `apps/auth-api/src/Auth.Infrastructure/Migrations/` -- new EF Core migration for `users` + `user_credentials` (via the `<svc>-migrate` bundle, AD-12 — do not run `dotnet ef database update` against the app's own connection).
- `apps/auth-api/src/Auth.Api/Program.cs` -- currently only health stubs; add `app.MapGroup("/api/auth")` with `POST /register`, DI wiring for the new ports, and a real `/health/ready` pending-migrations check.
- `libs/MyCollection.ServiceDefaults/` -- currently an empty shell; add the `ErrorKind` → ProblemDetails mapping (AD-13) that `Auth.Api`'s endpoint needs to turn a `Result` failure into a response.
- `apps/auth-api/openapi/auth-api.json` -- currently only `/health/*`; regenerate once `/api/auth/register` exists.
- `apps/auth-api/tests/Auth.UnitTests/Features/Register/RegisterUseCaseTests.cs` -- new; fakes all ports.
- `apps/auth-api/tests/Auth.IntegrationTests/RegisterEndpointTests.cs` -- new; `WebApplicationFactory` + Testcontainers PostgreSQL, covers the I/O matrix above.
- `apps/auth-api/tests/Auth.ArchitectureTests/LayeringTests.cs` -- modify; drop `.WithoutRequiringPositiveResults()` for the rules now exercised by real classes.

## Tasks & Acceptance

**Execution:**
- [x] `apps/auth-api/src/Auth.Domain/User.cs` -- add entity -- Domain owns the business object, no I/O
- [x] `apps/auth-api/src/Auth.Application/Abstractions/*.cs` -- add `IUserRepository`, `IPasswordHasher`, `IUnitOfWork` ports -- Application orchestrates through ports only
- [x] `apps/auth-api/src/Auth.Application/Features/Register/*.cs` -- add `RegisterCommand` + `RegisterUseCase` -- normalize, hash, persist, one transaction
- [x] `apps/auth-api/src/Auth.Infrastructure/AuthDbContext.cs` + `Entities/*.cs` -- add `User`/`user_credentials` mapping -- EF Core stays confined to Infrastructure (AD-2)
- [x] `apps/auth-api/src/Auth.Infrastructure/Repositories/UserRepository.cs` -- implement port, translate `23505` to `Conflict` -- AD-11
- [x] `apps/auth-api/src/Auth.Infrastructure/Security/BcryptPasswordHasher.cs` -- implement port with `EnhancedHashPassword` -- AD-10 (bcrypt only), no 72-byte truncation
- [x] `apps/auth-api/src/Auth.Infrastructure/UnitOfWork.cs` -- implement port -- one use case = one transaction
- [x] `apps/auth-api/src/Auth.Infrastructure/Migrations/` -- add migration -- schema for `users` + `user_credentials`
- [x] `libs/MyCollection.ServiceDefaults/` -- add `ErrorKind` → ProblemDetails mapping -- AD-13, shared across services
- [x] `apps/auth-api/src/Auth.Api/Program.cs` -- add `/api/auth/register` endpoint + DI wiring + real `/health/ready` check -- AD-12, AD-13
- [x] `apps/auth-api/openapi/auth-api.json` -- regenerate -- AD-16, commit the diff
- [x] `apps/auth-api/tests/Auth.UnitTests/Features/Register/RegisterUseCaseTests.cs` -- unit-test the I/O matrix's use-case-level cases -- fakes all ports
- [x] `apps/auth-api/tests/Auth.IntegrationTests/RegisterEndpointTests.cs` -- integration-test the I/O matrix end-to-end -- real Postgres via Testcontainers
- [x] `apps/auth-api/tests/Auth.ArchitectureTests/LayeringTests.cs` -- tighten layering assertions now that real classes exist
- [x] `apps/auth-api/db/init.sql` + `docker-compose.yml` -- add the app role, grants and mount -- AD-12, least privilege
- [x] `docs/auth.md` + `docs/conventions.md` + `docs/database.md` + `docs/README.md` + `docs/local-development.md` -- record the 8–32 character password policy and the `EnhancedHashPassword`/`EnhancedVerify` choice; write `database.md`; add the index row and link -- CLAUDE.md "Keeping docs current"; do this last, after code review, so the docs describe the code as merged

**Acceptance Criteria:**
- Given a fresh database, when `POST /api/auth/register` is called with a new email and a valid password, then a `users` row and a `user_credentials` row (bcrypt hash, not plaintext) are created and the response is `201` with `{ id, email }`.
- Given an email already in `users`, when registering again with any casing/whitespace variant of that email, then the response is `409` with `code: email_already_registered`.
- Given the migration has not been applied, when `Auth.Api` starts, then it never calls `Database.Migrate()` and `/health/ready` reflects pending migrations.
- Given a fresh `auth-db` volume, when `docker compose up` completes, then `POST /api/auth/register` succeeds as `auth_app`, and `auth_app` cannot run DDL.
- Given the build runs, then `apps/auth-api/openapi/auth-api.json` reflects `/api/auth/register` with no uncommitted diff.

## Implementation Notes

## Design Notes

- Persisting two tables: `User` (Domain) has no hash field. `IUserRepository.AddAsync(User user, string passwordHash)` lets Infrastructure create the `user_credentials` row (unique `user_id`, matching the spine's one-to-one) in the same `SaveChanges`.
- Duplicate email is checked in two places. The use case calls `EmailExistsAsync` first, which makes the rule visible where it belongs and skips the bcrypt cost for a known duplicate. The unique index stays the race-safe source of truth: `UnitOfWork` turns any Postgres `23505` into `CommitOutcome.UniqueViolation`, and the use case decides what that means. This is a return value, not an exception, per `docs/conventions.md` § Errors. Trade-off: `UnitOfWork` no longer matches the constraint name, so any unique violation during register reads as a duplicate email. The only other unique keys a register insert touches are freshly generated UUIDv7 ids, so this is accepted.
- Edge validation (email format, password 8–32 by `string.Length`, no trimming of the password) returns the same `Error("validation_failed", ErrorKind.Validation, ...)` through the shared mapper, so 400 bodies carry `code` and `traceId` like every other error (AD-13). Bind the request with nullable fields so a missing/null field is a 400, not a framework error.
- Startup DB/health wiring must be skipped when the OpenAPI generator runs the app (`docs/conventions.md`).
- `init.sql` is plain SQL, so it reads the role name and password with psql's `\getenv` (psql 15+) from the container env, keeping secrets out of the file. The entrypoint runs it only when the data volume is empty; later edits need `docker compose down -v` or a manual `psql` run.
- Integration tests may apply the migration with `MigrateAsync()` in the test fixture only; app code never does.

## Verification

**Commands:**
- `dotnet test MyCollection.slnx` -- expected: all green; integration tests need Docker running (Testcontainers).
- `dotnet build apps/auth-api/src/Auth.Api` -- expected: `apps/auth-api/openapi/auth-api.json` contains `/api/auth/register`; commit the diff.

**Manual checks:**
- `docker compose down -v && docker compose up --build`, then register via `http://localhost:8080/api/auth/register` -- expected: `201`; as `auth_app`, `CREATE TABLE x()` fails with permission denied.

## Spec Change Log

- 2026-10-10 -- Moved the duplicate-email decision from Infrastructure to the use case (user renegotiated the frozen intent). Added `IUserRepository.EmailExistsAsync`. Changed `IUnitOfWork.CommitAsync` from `Task<Error?>` to `Task<CommitOutcome>`. `UnitOfWork` no longer references `RegisterUseCase` or the email index name. This avoids every future unique rule piling up as a feature-specific `catch` in the shared `UnitOfWork`. KEEP: the unique index as the race-safe source of truth, the 409 contract, and one transaction per use case.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Email >254 chars would 500 (no test) | medium | patch | Check existed; added test, passes |
| 34-char over-limit test vs matrix 33 | low | patch | Test now uses 33 |
| No concurrent duplicate test | medium | patch | Added; one 201 + one 409 |
| Fixture dispose NRE | low | patch | Null-guarded |
| openapi not in diff | false | - | File excluded from the review diff by me; regenerated and contains /api/auth/register |
| ErrorResults throws on unmapped kind | false | - | Loud failure for an unreachable state |
| Translation in UnitOfWork, not repository | low | rejected | Port design: errors surface at commit; recorded in final summary |
| Malformed JSON lacks code; init.sql/role untested; DB-outage 503; UTF-16 length; citext; enumeration; ErrorResults tests | medium/low | defer | See deferred-work.md |

