---
title: 'MyCollection platform — repo and service scaffolding'
type: 'chore'
created: '2026-09-27'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '9df798a234c8f3d24925349342fcfc4fc055de1b'
context: ['{project-root}/_bmad-output/planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The repo has no code yet — no Nx workspace, no .NET solution, no service or gateway skeleton, no compose topology. Nothing exists for CAP-1..8 implementation to start from.

**Approach:** Stand up the full foundation described in the architecture spine's Structural Seed and stack table: Nx workspace root tooling, the .NET solution and Central Package Management, empty hexagonal project skeletons for auth-api (wired to a passing ArchitectureTests suite), the gateway skeleton, the compose topology (base + dev override), CI, and the dev JWT-key bootstrap script. No capability business logic (CAP-1..8) is written — every service/use-case class stays a scaffold for the first CAP to fill in.

**Decisions:**
- `libs/MyCollection.ServiceDefaults` is an empty shell for this setup: the class library project exists and is referenced by `Auth.Api`, but JWT validation, `ErrorKind`→ProblemDetails mapping, forwarded headers and Serilog wiring are written later, alongside CAP-1, once there is a real service to wire them against. The exception is health checks: AD-19 requires compose to gate gateway startup on `auth-api`'s `/health/ready`, so `Auth.Api` gets minimal `/health/live` and `/health/ready` endpoints directly (not in ServiceDefaults) that report healthy unconditionally — there's no DbContext yet to add a real pending-migrations check, so that refinement waits for CAP-1.
- `libs/MyCollection.Results` is implemented now in full: `Result<T>`, `Error(Code, Kind, Message)`, and the fixed `ErrorKind` enum (`Validation`, `Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `RateLimited`, `Unavailable`) per AD-4. It is framework-free with zero dependencies, so building it ahead of CAP work is low-risk and every CAP needs it immediately.

## Boundaries & Constraints

**Always:**
- Follow the spine's Structural Seed source tree, Consistency Conventions table, and pinned Stack versions exactly — do not introduce package versions outside `Directory.Packages.props` or deviate from the folder/naming conventions.
- Every new .NET project follows AD-2/AD-5: `net10.0`, nullable + warnings-as-errors via `Directory.Build.props`, versions only in `Directory.Packages.props`.
- `apps/auth-api/tests/Auth.ArchitectureTests` is created and wired to run against a Debug build (AD-3), even though there is nothing but empty layers to assert yet — assert the layering rules that already apply (Domain has no references, Application doesn't reference EF Core/ASP.NET/Infrastructure, Api doesn't reference `DbContext`).
- Compose follows AD-19: `docker-compose.yml` is the real topology (Production-shaped), `docker-compose.override.yml` holds only dev differences (watch mode, direct ports).
- Migrations tooling follows AD-12 (bundle + `<svc>-migrate` compose service), even though there are no migrations yet — the wiring exists so CAP-1 only has to add a migration.
- `.env.example` is committed; `.env` stays gitignored. No secrets committed.

**Never:**
- No CAP-1..8 business logic: no endpoints, use cases, entities, repositories, DbContext with a real model, or OAuth handler configuration. Layer projects may contain a placeholder file only if the SDK/dotnet CLI requires one to produce a valid project (e.g. an empty class removed once).
- No wishlist-api, cards-bff, MongoDB, Redis, or web UI (spec non-goals; spine confirms addable later without restructuring).
- Do not implement real JWT validation or ProblemDetails mapping inside `MyCollection.ServiceDefaults` — it stays an empty shell except for the minimal health stub noted in Decisions.
- Do not generate or commit a real JWT private key or OAuth secrets — only the `tools/` script that generates one locally.

</frozen-after-approval>

## Code Map

- `_bmad-output/planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md` -- binding source for every path, version and rule below (Structural Seed, Stack table, AD-1..22, Consistency Conventions).
- Repo root is currently empty of code (`nx.json`, `package.json`, `apps/`, `libs/` all absent) — this is a from-scratch scaffold, not a merge into existing structure.

## Tasks & Acceptance

**Execution:**
- [x] `global.json`, `.config/dotnet-tools.json` -- pin .NET SDK `10.0.401` and `dotnet-ef` `10.0.12`; set `"test": { "runner": "Microsoft.Testing.Platform" }` -- AD-5
- [x] `Directory.Build.props` -- `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true` for every project -- AD-5
- [x] `Directory.Packages.props` -- Central Package Management with every version from the spine's Stack table (EF Core/Npgsql, JwtBearer, Google/Discord auth, BCrypt.Net-Next, OpenApi/ApiDescription.Server, Scalar.AspNetCore, Serilog.AspNetCore, HealthChecks.EntityFrameworkCore, xunit.v3, Mvc.Testing, Testcontainers.PostgreSql, ArchUnitNET.xUnitV3) -- AD-5
- [x] `nx.json`, `package.json` -- Nx `23.2.1`, `@nx/dotnet` `23.2.1`, Node 24 LTS engines field -- stack table
- [x] `MyCollection.slnx` -- solution file referencing every project created below -- Structural Seed
- [x] `libs/MyCollection.Results/MyCollection.Results.csproj` + `Result.cs`, `Error.cs`, `ErrorKind.cs` -- framework-free class library, no PackageReferences; `Result<T>`, `Error(Code, Kind, Message)`, fixed `ErrorKind` enum -- AD-4
- [x] `libs/MyCollection.ServiceDefaults/MyCollection.ServiceDefaults.csproj` -- ASP.NET-facing class library, empty shell (no JWT/ProblemDetails/health-check code yet), referenced by `Auth.Api` -- AD-4
- [x] `apps/auth-api/src/{Auth.Domain,Auth.Application,Auth.Infrastructure,Auth.Api}/*.csproj` -- four projects, references only as the paradigm table dictates (Domain: none; Application: Domain + MyCollection.Results; Infrastructure: Application + Domain; Api: all three + libs/*) -- AD-2
- [x] `apps/auth-api/src/Auth.Api/Program.cs` -- composition root wiring `/health/live` and `/health/ready` (unconditionally healthy stub, no DB check yet — no DbContext exists) and `Microsoft.Extensions.ApiDescription.Server` output (`--file-name auth-api`) so `apps/auth-api/openapi/auth-api.json` is generated on build -- AD-16, AD-19
- [x] `apps/auth-api/tests/Auth.ArchitectureTests/*.csproj` + one ArchUnitNET test class -- asserts Domain/Application/Api layering rules from AD-3; project sets `Configuration=Debug` explicitly (or CI invokes it with `-c Debug`) so ArchUnitNET issue #498 doesn't drop async dependencies
- [x] `apps/auth-api/tests/{Auth.UnitTests,Auth.IntegrationTests}/*.csproj` -- empty test project skeletons (xUnit v3), `Auth.IntegrationTests` referencing `Testcontainers.PostgreSql` and `Microsoft.AspNetCore.Mvc.Testing` -- AD-20
- [x] `apps/auth-api/Dockerfile` -- `dev`, `migrate`, `final` targets, repo-root build context, `final` on `aspnet:10.0-alpine` -- AD-12, AD-19
- [x] `apps/gateway/project.json` -- Nx project file for the gateway app -- Structural Seed
- [x] `apps/gateway/templates/nginx.conf.template`, `apps/gateway/Dockerfile` -- CORS `map`, rate-limit zones (`10r/s burst=20` global; `5r/m burst=5` on `/api/auth/login|register`) with `limit_req_status 429`, `X-Request-Id` from `$request_id`, JSON access log, `resolver 127.0.0.11`, forwarded-header overwrite, static `application/problem+json` bodies for gateway-produced errors (404/429/502/503), no `/health/*` or `/.well-known/*` exposure -- AD-14, AD-15, AD-18
- [x] `docker-compose.yml` -- gateway (port 8080 only), auth-api, auth-db (Postgres 18), auth-migrate (`service_completed_successfully` dependency), `wget`-based health probes on `auth-api` and startup ordered on `service_healthy` (gateway waits on auth-api healthy), fixed internal subnet, named volume for Data Protection keys and Postgres data, `ASPNETCORE_ENVIRONMENT=Production` -- AD-1, AD-12, AD-19
- [x] `docker-compose.override.yml` -- `ASPNETCORE_ENVIRONMENT=Development`, `dev` Dockerfile target, `DOTNET_USE_POLLING_FILE_WATCHER=1`, anonymous `bin`/`obj` volumes, direct ports (auth-api `5001`, Postgres `5432`) -- AD-19
- [x] `.env.example` -- every env var the compose files reference, no real secrets -- AD-17
- [x] `tools/generate-dev-jwt-key.*` -- script that generates a local RS256 PEM keypair for dev, never committed -- AD-17, CAP-8
- [x] `apps/auth-api/openapi/auth-api.json` -- regenerated by a real `dotnet build`; committed content is now the genuine build output, not a placeholder -- AD-16
- [x] `.github/workflows/ci.yml` -- checkout with `fetch-depth: 0`, `nrwl/nx-set-shas`, `nx affected -t build test`, OpenAPI drift check (`git diff --exit-code -- 'apps/*/openapi'`), `nginx -t` -- AD-21

**Acceptance Criteria:**
- Given a clean clone with `.env` filled from `.env.example`, when `docker compose up` runs, then Postgres starts, `auth-migrate` completes (even with zero migrations), `auth-api`'s `/health/ready` stub reports healthy and compose marks it `service_healthy`, and `gateway` then starts, in that dependency order (CAP-8's mechanics, not its content) — **confirmed** 2026-09-27, after fixing the three bugs in Implementation Notes.
- Given the `Auth.Api` build output, when `apps/auth-api/openapi/auth-api.json` is diffed against a fresh generation, then there is no drift — **confirmed**: the committed file is itself the real build output.
- Given the workspace, when `nx run-many -t build test` runs (all non-gateway projects), then the solution builds and `Auth.ArchitectureTests`, `Auth.UnitTests` and `Auth.IntegrationTests` all pass — **confirmed** on this machine (10/10 tests green).
- Given `apps/auth-api/src/Auth.Application`, when its `.csproj` is inspected, then it has no `PackageReference` to EF Core or ASP.NET Core packages.

## Implementation Notes

**All files scaffolded (2026-09-27). Build and non-Docker tests are now verified green. Only Docker-dependent verification remains — resume here.**

Confirmed without a compiler (first pass, before the .NET SDK was installed):
- All new JSON files parse; all `.csproj`/`.props`/`.slnx` files are well-formed XML.
- `docker compose config` (with `.env` copied from `.env.example`) merges `docker-compose.yml` + `docker-compose.override.yml` with no errors.
- `.gitignore` already covers `.env`, `*.pem`, `keys/` — the JWT key script's output won't get committed.
- The gateway's `nginx.conf.template` had a real bug, found and fixed during this session: the `location = /api/auth/login` and `/api/auth/register` blocks answered CORS preflight and proxied requests **without** the origin-allowlist check or the OPTIONS CORS headers the general `/api/auth/` block has, so a real browser preflight to those two endpoints would have failed silently and disallowed origins wouldn't have gotten the required 403. Fixed by collapsing both into one `location ~ ^/api/auth/(login|register)$` regex block carrying the same origin-rejection logic as `/api/auth/`, plus **both** rate-limit zones explicitly (`limit_req` in a location does not merge with the server-level directive — it replaces it once redeclared, so `global_rl` had to be repeated alongside `auth_rl`).

**Confirmed with a real .NET 10 SDK + Node 24 (2026-09-27, later same day):**
- `dotnet restore` + `dotnet build MyCollection.slnx -c Debug`: builds clean, 0 warnings/errors, after fixing three real compile issues found only by actually compiling:
  - `LayeringTests.cs`: `Assembly` was ambiguous between `ArchUnitNET.Domain.Assembly` and `System.Reflection.Assembly` — qualified the four `Assembly.Load(...)` calls with `System.Reflection.`.
  - `LayeringTests.cs`: this ArchUnitNET version's `ResideInNamespace(string)` takes no regex flag and matches only an exact namespace, and `IObjectProvider<T>` has no `.Or(...)` extension to combine already-built providers. Replaced `ResideInNamespace("Microsoft.EntityFrameworkCore.*", true)` with `HaveFullNameContaining("Microsoft.EntityFrameworkCore")` (matches nested namespaces via substring), and replaced `providerA.Or(providerB)` with combining predicates at the `.That()` level instead (`Types().That().ResideInAssembly(...).Or().ResideInAssembly(...)`), which is how this library's fluent API actually composes multiple conditions into one `IObjectProvider`.
  - `HealthEndpointTests.cs`: xUnit analyzer `xUnit1051` (treated as an error via `TreatWarningsAsErrors`) required `client.GetAsync(path, TestContext.Current.CancellationToken)` instead of the two-arg-less overload.
  - Also found and fixed at test-run time (not compile time): ArchUnitNET fails a rule by default if its "given" predicate matches **zero** types ("requires positive evaluation" — a typo-catching safety net), which is exactly the state of every layer right now since nothing but the empty scaffolds exist. Added `.WithoutRequiringPositiveResults()` to all five rules in `LayeringTests.cs` so they pass vacuously today and start enforcing for real the moment CAP-1 adds actual classes.
- `apps/auth-api/openapi/auth-api.json` was regenerated by the real build (`Microsoft.Extensions.ApiDescription.Server` wiring in `Auth.Api.csproj` works as designed) and the committed file is now genuine build output, not the earlier hand-authored placeholder.
- `npm install` at the repo root succeeded (`package-lock.json` is new and should be committed alongside the rest). `npx nx show projects` correctly discovers all 9 .NET projects via `@nx/dotnet` plus the `gateway` Nx project.
- `npx nx run-many -t build --exclude=gateway` and `npx nx run-many -t test --exclude=gateway`: **all green** — `Auth.UnitTests` (1 test), `Auth.ArchitectureTests` (7 tests), `Auth.IntegrationTests` (2 tests, health endpoints via `WebApplicationFactory` — no Testcontainers usage yet so no Docker needed for these specifically) all pass. This is real confirmation of the "solution builds, ArchitectureTests passes" acceptance criterion.
- `npm audit` flags 2 high-severity advisories, both transitive dev-tooling (`smol-toml` via `nx`), fixable only by downgrading to `nx@22.6.4` — not applied, since it would violate the spine's pinned Nx `23.2.1` and this is a build-time-only dependency, not shipped code. Flagged here rather than silently ignored.

**Docker verification (2026-09-27, closing this build):**
- `docker build -f apps/gateway/Dockerfile` then `nginx -t` inside the image: initially failed with `host not found in upstream "auth-api"` — the two `proxy_pass http://auth-api:8080;` lines used a literal hostname, which nginx resolves once via the system resolver at config-load time. AD-14's `resolver 127.0.0.11` only defers resolution to request time for a **variable** upstream. Fixed both call sites with `set $auth_upstream "http://auth-api:8080"; proxy_pass $auth_upstream;`. `nginx -t` passes clean after the fix.
- `docker compose up --build`: initially failed at the `auth-migrate`/`auth-api` image build with a NuGet `FallbackPackagePathResolver` error — `.dockerignore`'s bare `bin/`/`obj/` patterns only match at the build-context root (unlike `.gitignore`, which matches any depth by default), so the host's per-project `obj/project.assets.json` (built with Windows-only NuGet paths) was leaking into the Linux build context. Fixed with `**/bin/`/`**/obj/`.
- After both fixes, the build succeeded but `auth-api` never went `healthy` — its compose healthcheck used `wget --spider`, which sends a `HEAD` request; the Minimal API health endpoints are `MapGet`-only and answer `HEAD` with `405`. Fixed by dropping `--spider` for a real `GET` to `/dev/null`.
- Re-ran `docker compose up -d` after all three fixes: confirmed startup order `auth-db healthy` → `auth-migrate` completed (0 migrations, no-op) → `auth-api healthy` → `gateway` started. Confirmed end-to-end routing: `GET http://localhost:8080/api/auth/whatever` returns auth-api's own empty-body `404` (proxied through, not answered by the gateway), `GET http://localhost:8080/health/live` returns the gateway's static `404` (never exposed publicly, per AD-14), and direct port `5001` reaches auth-api's health endpoints directly. All three fixes are documented in [docs/local-development.md § Gotchas found during scaffolding](../../../../docs/local-development.md#gotchas-found-during-scaffolding).
- All acceptance criteria are now confirmed, including the `docker compose up` startup-order one that was previously only reasoned about.

To resume further capability work: run `bmad-build` for CAP-1 (register) against `spec-mycollection-platform`.

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `nx affected -t build test` -- expected: solution builds; `Auth.ArchitectureTests`, `Auth.UnitTests`, `Auth.IntegrationTests` all report zero tests failed (empty or scaffold-only suites)
- `docker compose config` -- expected: merges `docker-compose.yml` + `docker-compose.override.yml` without error
- `nginx -t` (inside the gateway image) -- expected: config syntax OK

**Manual checks (if no CLI):**
- Inspect `MyCollection.slnx` opens in the IDE with all projects visible and correctly nested under `apps/`/`libs/`.
