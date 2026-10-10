---
title: 'Split register name into first name and last name; squash migrations'
type: 'refactor'
created: '2026-10-09'
status: 'done'
baseline_commit: 'c0438d2babc5e2597a5aa4511c14a5c783e873da'
route: 'dispatch'
review_loop_iteration: 0
context: ['{project-root}/docs/conventions.md', '{project-root}/docs/auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Registration captures a single `name`. The product needs `firstName` and `lastName` as separate values. The schema has one migration (`InitialUsers`) that bakes in the single `name` column.

**Approach:** Replace `name` with `firstName` and `lastName` through the whole register slice (request, command, result, response, `User`, EF mapping, tests, docs, OpenAPI). Because nothing is deployed, delete the existing migration, drop the local dev database, and regenerate one fresh initial migration so the repo has exactly one migration.

## Boundaries & Constraints

**Always:** Both fields are `[Required]` on register, `MaxLength(100)` each. `users` columns `first_name` / `last_name` are `varchar(100)`, nullable at DB level (non-password sign-ups stay possible later), like the old `name`. Request stays `multipart/form-data`. Regenerate and commit `apps/auth-api/openapi/auth-api.json`. Migration is generated with `dotnet ef migrations add` and applied only through the `auth-migrate` bundle. Update `docs/auth.md` and any other doc that mentions `name` for register.

**Never:** No data-preserving migration or backfill. No `Database.Migrate()`. Do not touch the avatar handling. Do not drop anything beyond the local docker compose auth DB volume. Do not edit the JWT `name` claim text in `docs/auth.md` beyond noting it is derived from first + last name when tokens are issued (no token code exists yet).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | email, password, firstName, lastName | 201 with `{id, email, firstName, lastName}`; row stores both columns | N/A |
| Missing first or last name | field absent or empty | 400 ProblemDetails, validation `code`, error keyed to the missing field | AD-13 shape via ServiceDefaults helper |
| Too long | firstName or lastName > 100 chars | 400 as above | same |
| Legacy `name` field sent | `name` only | 400, firstName/lastName required | same |

</frozen-after-approval>

## Code Map

- `apps/auth-api/src/Auth.Domain/User.cs` -- `Name` → `FirstName`, `LastName`; `Register(email, firstName, lastName, now)`.
- `apps/auth-api/src/Auth.Application/Features/Register/RegisterCommand.cs`, `RegisterUseCase.cs` -- command/result carry both names; `user.Name!` usage goes away.
- `apps/auth-api/src/Auth.Api/Endpoints/RegisterEndpoint.cs` -- `RegisterRequest` / `RegisterResponse`, `[Required, MaxLength(100)]`.
- `apps/auth-api/src/Auth.Infrastructure/Entities/UserConfiguration.cs` -- map `first_name`, `last_name` (`HasMaxLength(100)`).
- `apps/auth-api/src/Auth.Infrastructure/Migrations/` -- delete `20261007192617_InitialUsers*.cs` and `AuthDbContextModelSnapshot.cs`, regenerate one migration.
- `apps/auth-api/tests/Auth.UnitTests/Features/Register/RegisterUseCaseTests.cs`, `Auth.IntegrationTests/RegisterEndpointTests.cs` -- form fields at lines ~130, 177, 192; asserts at ~29, 38.
- `apps/auth-api/openapi/auth-api.json` -- regenerate.
- `docs/auth.md` (line ~63 and JWT claims line ~10) -- update.
- `docker-compose.yml` -- volume `auth-db-data`; `init.sql` only runs on an empty volume, so `docker compose down -v` recreates roles too.

## Tasks & Acceptance

**Execution:**
- [ ] `Auth.Domain/User.cs`, `Auth.Application/Features/Register/*`, `Auth.Api/Endpoints/RegisterEndpoint.cs`, `Auth.Infrastructure/Entities/UserConfiguration.cs` -- replace `Name` with `FirstName`/`LastName` -- the feature itself
- [ ] `Auth.Infrastructure/Migrations/*` -- delete old migration and snapshot, run `dotnet ef migrations add InitialUsers` (project/startup per docs/local-development.md) -- single migration
- [ ] local dev DB -- `docker compose down -v`, then `docker compose up -d auth-db auth-migrate` -- drop and recreate DB from the new migration
- [ ] unit and integration tests -- update for two fields, add missing/too-long cases -- cover the matrix
- [ ] `apps/auth-api/openapi/auth-api.json`, `docs/auth.md` (+ other docs mentioning register `name`) -- regenerate/update

**Acceptance Criteria:**
- Given a clean checkout, when migrations are listed, then exactly one migration exists and its `users` table has `first_name` and `last_name` and no `name`.
- Given the recreated DB, when `auth-migrate` runs, then it applies cleanly and `/health/ready` is healthy.
- Given valid input, when POST `/register`, then 201 returns `firstName` and `lastName` and the row stores them.
- Given the build, when running `dotnet build`, then there are no errors and no remaining references to the single `Name` in the register slice.

## Implementation Notes

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence / route |
|---|---|---|
| `errors` keys never asserted | medium | patched: tests assert keys. Real keys are PascalCase (`FirstName`), not the camelCase form fields |
| Whitespace-only names untested | low | patched: two InlineData rows, both 400 |
| Length-bound test convoluted, 2nd response unchecked | low | patched: split into first/last theories |
| Migration startup-project mismatch (spec vs doc) | low | patched: `Auth.Api` has no EF Design package, spec now matches doc |
| Names not trimmed / no domain invariant | low | defer: pre-existing pattern (old `name`), same as before |
| Unbounded avatar upload on anonymous route | medium | defer: pre-existing from the validation-annotations work, not this change |
| JSON body now 415 untested | low | defer: pre-existing from the multipart change |
| OpenAPI 400 body shape, traceId format, docs/conventions wording | low | defer: pre-existing, not this change |
| Old spec says `name`, CAP-1 spec stale | low | false-ish: historical specs, not current behaviour |
| Mononym / i18n, CLAUDE.md cadence, Next steps.txt | false | product/housekeeping, outside the intent |

## Verification

**Commands:**
- `dotnet build MyCollection.slnx` -- expected: succeeds
- `dotnet ef migrations list --project apps/auth-api/src/Auth.Infrastructure --startup-project apps/auth-api/src/Auth.Infrastructure` -- expected: one migration
- `docker compose up -d auth-db auth-migrate` -- expected: `auth-migrate` exits 0
