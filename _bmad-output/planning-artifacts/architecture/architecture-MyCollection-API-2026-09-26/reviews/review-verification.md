# Review — Verification lens (reality-check of committed decisions)

- **Spine reviewed:** `../ARCHITECTURE-SPINE.md` (draft, 2026-09-26)
- **Reviewer lens:** is every committed decision checked against current reality (registries, vendor docs, open issues), or only asserted from memory?
- **Date of checks:** 2026-09-26 (NuGet flat-container API, npm registry, Docker Hub API, dotnet release metadata, Node release schedule, vendor docs, GitHub issues, and the `@nx/dotnet@23.2.1` package contents)

## Verdict

**The Stack table holds up. The integration assumptions around it need work.** All 19 pinned versions exist and are the current stable releases. Every named technology still exists and targets `net10.0`. However, four decisions rely on behaviour that the real tools don't provide by default. If they are left as written, the first slice will hit build or runtime failures: JWKS-only validation (AD-6), `dotnet test` with xunit.v3 4.x (AD-5/AD-20), compose health checks (AD-19), and startup code running during OpenAPI and EF design-time generation (AD-16/AD-12/AD-17). There is also one known ArchUnitNET defect that can make AD-3 pass without checking anything.

## 1. Stack table: registry verification

| Item | Spine | Registry reality (2026-09-26) | Status |
|---|---|---|---|
| .NET SDK | 10.0.401 | `latest-sdk` 10.0.401 (release 2026-09-08) | OK, current |
| .NET / ASP.NET Core runtime | 10.0.12 | `latest-release` 10.0.12 | OK, current |
| Nx | 23.2.1 | npm `latest` = 23.2.1 (`next` 23.3.0-beta.5) | OK |
| @nx/dotnet | 23.2.1 **(experimental)** | 23.2.1 exists. **The Nx 23 release blog says "@nx/dotnet graduates out of experimental in v23"** | Version OK; label is **wrong** (see F-8) |
| Node.js | 24 LTS | v24 moves to *maintenance* on 2026-10-20; v26 becomes Active LTS on 2026-10-28 | OK for now; note it (F-9) |
| nginx | nginx:1.30-alpine (1.30.5) | Tags `1.30-alpine`, `1.30.5-alpine*`, `stable-alpine` exist; mainline is 1.31.6 | OK |
| postgres | postgres:18-alpine | `18-alpine` → 18.6. 19 is at beta4 only | OK |
| EF Core / Design | 10.0.12 | latest stable 10.0.12 (11.0 at rc.1) | OK |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | latest stable 10.0.3 | OK |
| JwtBearer / Google | 10.0.12 | 10.0.12 | OK |
| AspNet.Security.OAuth.Discord | 10.0.0 | 10.0.0 (published 2025-11-11), `net10.0` + FrameworkReference AspNetCore.App | OK |
| BCrypt.Net-Next | 4.2.1 | latest stable 4.2.1 (5.0 prereleases only) | OK |
| Microsoft.AspNetCore.OpenApi / ApiDescription.Server | 10.0.12 | 10.0.12 both | OK |
| Scalar.AspNetCore | 2.17.10 | 2.17.10 (published 2026-09-25), has `net10.0` | OK. Released yesterday, so Dependabot will churn it |
| Serilog.AspNetCore | 10.0.0 | 10.0.0 stable (10.0.1-dev only) | OK |
| HealthChecks.EntityFrameworkCore | 10.0.12 | 10.0.12 | OK |
| xunit.v3 | 4.0.1 | 4.0.1 (4.0.0 published 2026-08-15) | OK, but see F-2 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | 10.0.12 | OK |
| Testcontainers.PostgreSql | 4.15.0 | 4.15.0, has `net10.0` | OK |
| TngTech.ArchUnitNET.xUnitV3 | 0.13.4 | 0.13.4 (2026-08-20). Depends on `xunit.v3.assert >= 1.1.0` | Resolves, but 4.x compatibility is **unverified upstream** (F-5) |
| *(missing)* dotnet-ef tool | — | 10.0.12 | Needed for AD-12 bundles, but not pinned (F-10) |

Other named APIs:

- `Guid.CreateVersion7()` exists since .NET 9. OK. Npgsql 9+ also generates v7 GUID keys client-side by default, so the two agree.
- EF Core migration bundles (`dotnet ef migrations bundle`) are a stable feature since EF Core 6. The technology is OK, but the design-time caveat in F-4 applies.
- `Microsoft.Extensions.ApiDescription.Server` build-time generation is documented for .NET 10 (the default is OpenAPI 3.1). `@nx/dotnet`'s analyzer reads `OpenApiDocumentsDirectory` and `--file-name` and adds the JSON file to the build target's cache outputs. AD-16 therefore fits the Nx cache, which is a good sign. See F-4 and F-7 for the caveats.
- `@nx/dotnet` 23.2.1 detects `.csproj`/`.fsproj`/`.vbproj` with an MSBuild analyzer (needs SDK 8+). It reads `Directory.Build.props` and `Directory.Packages.props` as inputs, and wires `ProjectReference` edges into the graph, so `nx affected` works. It infers `build` (Debug by default, `:release` configuration), `test`, `publish`, `pack`, `run`, `watch`, `restore` and `clean`. It marks a project as a test project from `IsTestProject` or a `Microsoft.NET.Test.Sdk` / `Microsoft.Testing*` package reference. **Nx project names come from `MSBuildProjectName`** (`Auth.Api`, `Auth.UnitTests`), not the folder names (`auth-api`).

## 2. Technical claims

| Claim | Reality | Status |
|---|---|---|
| Browsers treat `http://localhost` as a secure context, so `Secure` cookies work | True for Chrome 89+, Edge and Firefox 75+. **Safari/WebKit discards `Secure` cookies set by http://localhost** (WebKit bug 281149; mdn/content#41366; webcompat #142566). This also affects Playwright WebKit | **Needs a caveat** (F-6) |
| SameSite treats different ports as same-site | True. A "site" is scheme plus registrable domain, and the port is ignored, so `localhost:5173` and `localhost:8080` are same-site. The scheme does matter (schemeful same-site): `http` and `https` are cross-site | OK |
| `SameSite=Strict` cookie set on the OAuth callback (a cross-site top-level redirect from Google/Discord) | RFC 6265bis allows cookies to be *set* on top-level navigations, and the later `fetch` to `/api/auth/refresh-token` is same-site, so the flow works. Separately, ASP.NET's own OAuth correlation/nonce cookies are `SameSite=None; Secure`, so they also break on Safari over http://localhost | OK, with the Safari caveat |
| nginx `auth_jwt` is Plus-only | Confirmed: nginx.org says `ngx_http_auth_jwt_module` "is available as part of our commercial subscription" | OK. It supports "the gateway performs no token validation" |
| .NET runtime images lack curl | Confirmed: `mcr.microsoft.com/dotnet/aspnet:10.0` is Ubuntu 24.04 without curl or wget, and chiseled images have no shell. `-alpine` variants have busybox `wget` | OK as a claim, but **the spine never says how the compose health check runs** (F-3) |

## 3. Findings

### F-1 — HIGH — AD-6: "standard JwtBearer handler, fetching the JWKS" doesn't work with only `/.well-known/jwks.json`

The JwtBearer handler's `ConfigurationManager<OpenIdConnectConfiguration>` fetches an **OIDC discovery document** (`/.well-known/openid-configuration`) and reads `jwks_uri` from it. It has no option to point straight at a JWKS URL (dotnet/aspnetcore#24309 is still the reference for this gap). The handler also sets `RequireHttpsMetadata = true` by default, which rejects `http://auth-api:8080`.

**Fix:** have auth-api also serve a minimal `GET /.well-known/openid-configuration` (`issuer`, `jwks_uri`). In ServiceDefaults, set `Authority`/`MetadataAddress` to the internal URL, set `RequireHttpsMetadata = false` for the internal network only, and set `ValidIssuer = "mycollection-auth"` explicitly. Otherwise the discovery doc's `issuer` must equal `mycollection-auth` exactly. Add the path to AD-6 and to AD-14's "never exposed publicly" list. The alternative is a custom `IssuerSigningKeyResolver` or JWKS-only `IConfigurationManager` in ServiceDefaults, but the spine must say which one.

### F-2 — HIGH — AD-5/AD-20: xunit.v3 4.x needs `dotnet test` in Microsoft Testing Platform mode, and `global.json` doesn't say so

xunit.v3 4.0 dropped MTP v1 and defaults to MTP v2. On the .NET 10 SDK, `dotnet test` on the VSTest path fails with "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later". The xunit docs tell you to opt in through `global.json`. `@nx/dotnet`'s inferred `test` target just runs `dotnet test`, so `nx affected -t build test` (AD-21) fails until this is set.

**Fix:** extend AD-5: "`global.json` pins the SDK **and** sets `"test": { "runner": "Microsoft.Testing.Platform" }`". Don't add `Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio` unless VS Test Explorer on old VS builds is needed. Spike this once: `nx show project Auth.UnitTests` should list a `test` target, and `nx run Auth.UnitTests:test` should pass.

### F-3 — HIGH — AD-19: `service_healthy` has no way to probe the service

AD-19 orders startup with `service_healthy` on `/health/ready`. The `final` image (`aspnet:10.0`, Ubuntu) has no curl or wget, so a `healthcheck: test: curl …` is always unhealthy and compose never starts the gateway.

**Fix:** decide it in AD-19. The options are:

- (a) base `final` on `aspnet:10.0-alpine` and use `wget -qO- http://localhost:8080/health/ready`
- (b) ship a tiny health-probe mode in the app (e.g. `dotnet Auth.Api.dll --healthcheck`), or a single-file probe binary
- (c) install curl in `final`, which enlarges the attack surface

(a) is the simplest, and it matches the Postgres and nginx images, which are already alpine.

### F-4 — MEDIUM — AD-16 / AD-12 / AD-17: the entry point runs at build time (OpenAPI) and at bundle time (EF design time)

MS Learn (aspnetcore-10.0): build-time OpenAPI generation "functions by launching the app's entrypoint with a mock server… any logic in the app's startup is invoked." The fix it documents is to guard with `Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider"`. `OpenApiGenerationEnvironment` only arrives in **.NET 11**, so under .NET 10 generation runs as Production with no `.env`. `dotnet ef migrations bundle` with `Auth.Api` as the startup project also builds the host. Combined with AD-17's fail-fast validation, reading the PEM key and Serilog bootstrap, both steps can fail in CI and in `docker build`.

**Fix:**

- Add to AD-16: "startup side effects (PEM load, options validation, DB connections) are skipped when the entry assembly is `GetDocument.Insider`".
- Add to AD-12: "Infrastructure provides an `IDesignTimeDbContextFactory` so bundles never boot the Api host". The bundle takes its connection from `--connection` or an environment variable at run time.
- Consider disabling doc generation inside Docker builds with `-p:OpenApiGenerateDocuments=false`.

### F-5 — MEDIUM — AD-3: ArchUnitNET can pass vacuously, and its xunit.v3 4.x support is unverified

- **Open issue TNG/ArchUnitNET#498 (filed 2026-09-24):** in Release builds, *every* dependency inside an `async` method is missing from the loaded architecture. Negative rules such as `NotDependOnAny` therefore **pass** on real violations. Most use cases and endpoints here are async. `@nx/dotnet` builds Debug by default, but a `-c Release` or `:release` test run would hollow out AD-3.
- `TngTech.ArchUnitNET.xUnitV3` 0.13.4 is compiled against `xunit.v3.assert 1.1.0`. With xunit.v3 4.0.1 it resolves `xunit.v3.assert 4.0.1`. The adapter only subclasses `Xunit.Sdk.XunitException(string)`, so it very likely works, but upstream has not validated it: the "update xunit-dotnet monorepo to v4" PR (#480) is still open.

**Fix:** extend AD-3:

1. Architecture tests run in the Debug configuration.
2. Each rule set includes one "canary" test that asserts a deliberately violating fixture type **fails**, which catches both vacuous passes and adapter breakage.
3. Back ArchUnitNET with a cheap project-graph check, such as an MSBuild or test assertion on `ProjectReference`/`PackageReference` per layer (e.g. no `Microsoft.EntityFrameworkCore*` in Application). This doesn't depend on IL analysis of async state machines.

### F-6 — MEDIUM — AD-9: `Secure` refresh cookie over http://localhost fails in Safari

For Chrome, Edge and Firefox, AD-9 works as written. Safari/WebKit discards `Secure` cookies from http://localhost, so refresh, logout and both OAuth flows (including ASP.NET's correlation cookies) break in Safari during development and in WebKit E2E runs.

**Fix:** add a line to AD-9/AD-19: "Local dev over http is supported on Chromium and Firefox only. Safari/WebKit testing needs HTTPS at the gateway (e.g. mkcert) and is deferred with TLS." Do *not* make `Secure` conditional on the environment.

### F-7 — LOW — AD-16: file name and drift check

The build-time document is named after the project file by default (`Auth.Api.json`), not `auth-api.json`. Set `<OpenApiDocumentsDirectory>../../openapi</OpenApiDocumentsDirectory>` and `<OpenApiGenerateDocumentsOptions>--file-name auth-api</OpenApiGenerateDocumentsOptions>`. "Committed" is only enforced if CI fails on drift, so add `git diff --exit-code apps/*/openapi` after build in AD-21.

### F-8 — LOW — Stack: `@nx/dotnet (experimental)` is outdated

Nx 23 made the plugin GA. Drop the label, which also removes a false risk signal. Note in Conventions that Nx project names are the MSBuild project names (`Auth.Api`), so `nx run auth-api:…` won't exist. Name any `apps/gateway/project.json` to match.

### F-9 — LOW — Node 24 enters maintenance on 2026-10-20

Node 24 is fine for Nx-only use. Either keep it and write "24 (Maintenance LTS from 2026-10-20)", or plan to move to 26 once it becomes LTS on 2026-10-28.

### F-10 — LOW — AD-15 and AD-12 details

- **AD-15:** in .NET 10, `ForwardedHeadersOptions.KnownNetworks` is obsolete (ASPDEPR005) in favour of `KnownIPNetworks`. With AD-5's warnings-as-errors, the old API breaks the build. Also, "trusting only the gateway's network" needs a **fixed compose subnet** (`networks: … ipam.config.subnet`). Docker assigns subnets dynamically otherwise.
- **AD-12:** pin `dotnet-ef` 10.0.12 in `.config/dotnet-tools.json` and add it to the Stack. When building the bundle, match the runtime identifier to the image (`linux-musl-x64` if alpine is chosen per F-3).
- **AD-21:** `nx affected` in GitHub Actions needs `fetch-depth: 0` plus `nrwl/nx-set-shas`, or base and head SHAs are wrong.
- **AD-5:** consider `"rollForward": "latestPatch"` or `"latestFeature"` in `global.json` so patch SDKs don't break contributors.

## 4. Confirmed without findings

- AD-1 (nginx has no JWT validation, because `auth_jwt` is Plus-only)
- AD-11 (UUIDv7 API)
- AD-12 (bundle feature exists; ordering with `service_completed_successfully` is valid Compose)
- Serilog and Scalar on `net10.0`
- Testcontainers 4.15 on `net10.0` with `postgres:18-alpine`
- SameSite port semantics
- Discord provider 10.0.0 on `net10.0`

## Sources

- NuGet flat-container and registration API (all packages above); npm registry (`nx`, `@nx/dotnet`); Docker Hub tags API (`nginx`, `postgres`); dotnetcli release metadata 10.0; nodejs/Release schedule.json
- Nx 23 release blog, https://nx.dev/blog/nx-23-release; `@nx/dotnet@23.2.1` tarball (MsbuildAnalyzer strings)
- MS Learn, "Generate OpenAPI documents" (aspnetcore-10.0), https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0
- xUnit.net MTP docs, https://xunit.net/docs/getting-started/v3/microsoft-testing-platform; xunit v3 4.0.0 release notes, https://xunit.net/releases/v3/4.0.0
- TNG/ArchUnitNET issues #480 and #498; source of `ArchUnitNET.xUnitV3`
- nginx `ngx_http_auth_jwt_module` docs, https://nginx.org/en/docs/http/ngx_http_auth_jwt_module.html
- WebKit Secure-cookie-on-localhost: mdn/content#41366, webcompat/web-bugs#142566, tauri-apps/wry#444
- .NET 10 breaking change on KnownNetworks, https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/10/ipnetwork-knownnetworks-obsolete
- dotnet/aspnetcore#24309 (JwtBearer JWKS endpoint); dotnet/AspNetCore.Docs#24341 and dotnet-docker discussion #4296 (no curl in runtime images)
