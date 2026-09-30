# Testing

Binding rules: spine AD-3 and AD-20. Framework: **xUnit v3** on the Microsoft Testing Platform (selected in `global.json`).

| Layer | Project | Scope | Real | Faked |
|---|---|---|---|---|
| Unit | `<Svc>.UnitTests` | One entity or use case | The code | All ports (in-memory repositories, fake clock, fake hasher) |
| Integration | `<Svc>.IntegrationTests` | One service, started in-process (`WebApplicationFactory`) | HTTP pipeline, EF Core, **PostgreSQL 18 via Testcontainers** | External providers (Google, Discord) |
| Architecture | `<Svc>.ArchitectureTests` | Layer dependencies (ArchUnitNET) | Compiled assemblies | nothing |

## Examples of what goes where

- **Unit:** "a consumed refresh token presented 5 s later yields a sibling in the same family"; "presented 11 s later revokes the family" (use `FakeTimeProvider`).
- **Integration:** "`POST /api/auth/login` with a wrong password returns 401 ProblemDetails with `code: invalid_credentials`"; "logout, then refresh, returns 401".
- **Architecture:** "`Auth.Domain` depends on nothing"; "`Auth.Application` does not depend on `Microsoft.EntityFrameworkCore`"; "`Auth.Api` endpoints do not use `DbContext`". Always run them against a **Debug** build (in Release, ArchUnitNET misses async dependencies, issue #498). They also check each layer's `.csproj` references.
- **Integration (concurrency):** two parallel refreshes with the same cookie must not fork the family.

## Status note

`Auth.ArchitectureTests`' five layering rules currently carry `.WithoutRequiringPositiveResults()`. ArchUnitNET fails a rule by default when its target set is empty ("requires positive evaluation," a typo-catching safety net), and every layer is still an empty scaffold — so without that call, all five would fail today for the wrong reason. Remove it layer by layer as CAP-1 adds real classes; until then these tests pass vacuously rather than asserting anything.

## Running

- `nx test <Project>` for one project, `nx affected -t test` for whatever a change touched. CI runs `nx affected -t build test`.
- Integration tests need **Docker running** (Testcontainers starts Postgres).

## Not yet

E2E tests through the gateway, including a mock OAuth2 server for Google/Discord, are deferred until slice 1 ships.
