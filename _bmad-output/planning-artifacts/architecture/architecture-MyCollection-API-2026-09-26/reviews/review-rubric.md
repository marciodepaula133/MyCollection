# Architecture Spine Review (rubric): MyCollection platform

- **Reviewed:** `../ARCHITECTURE-SPINE.md` (draft, 2026-09-26)
- **Against:** `specs/spec-mycollection-platform/SPEC.md`, `stack.md`, decision log `../.memlog.md`
- **Reviewer stance:** independent, good-spine checklist. The spec's `docker-compose.dev.yml`, Postgres 16 and `Jwt__Secret` are intentionally superseded and are not counted as findings.

## Verdict

**Approve after required revisions.** The spine is well-shaped: it fixes most of the real cross-unit divergence points (topology, layering, JWT contract, error shape, routing, config, logging, compose shape, tests), resolves every spec open question, and maps CAP-1..CAP-8. However, a few rules are **technically wrong or not implementable as written** (the refresh grace window, "standard JwtBearer against a JWKS", the OAuth callback mechanics, `Result<T>` placement) and would produce divergence or rework in the first stories. The operational envelope is thin: several dev-environment decisions from the log never made it into the spine, and there is no Open Questions section.

## Checklist scorecard

| Check | Result | Notes |
|---|---|---|
| Fixes the real divergence points for the level below | Mostly | Missing: OAuth callback path/claim mapping and post-callback redirect, JWKS discovery mechanics, `Result<T>` home, migrate image target, Nginx upstream resolution, email normalization (F1–F8) |
| Every AD Rule enforceable and prevents its divergence | Mostly | AD-8 is not implementable as written (F1). AD-13's "one place" mapping contradicts the layering table (F4). AD-16 "committed" has no drift check (F11). AD-3's "except composition-root" needs a concrete allow-list (F14) |
| Nothing Deferred can let two units diverge | Mostly | "How wishlist-api and cards-bff call each other" hides a service-to-service auth decision that touches the auth-api JWT contract (F15) |
| Covers CAP-1..CAP-8 | Yes, with gaps | CAP-5/6 success (the scripted run through Google/Discord) can't be verified with E2E deferred (F10). CAP-8 hot reload on a Windows host is at risk (F7) |
| Operational envelope decided, deferred or open | Partial | Environment matrix, HTTP-in-dev, Secure cookie over HTTP, PG18 volume path, container health probe, Data Protection keys, CI inputs: undecided and not listed (F9, F12, F13). No Open Questions section |
| Internal contradictions | Some | AD-8 vs hash-only storage; AD-13/AD-4 vs paradigm table; AD-12 vs AD-19 Dockerfile targets; AD-14 JWKS "public path" vs "never exposed" |

## Findings

### F1 — AD-8 grace window "returns the same successor" is impossible with hash-only storage [HIGH]

AD-8 stores refresh tokens **only as SHA-256 hashes**, yet requires that a consumed token presented again within 10 s "returns the same successor". The raw successor value no longer exists anywhere, so it can't be returned. Concurrency is also undecided: two tabs refreshing at the same millisecond both read `consumed_at IS NULL`. Without a row lock or optimistic concurrency, both mint successors, or one of them trips theft detection. Smaller points: a 30-day sliding window with no absolute family cap means a session can live forever, and the 15-minute access-token validity after logout is not stated as accepted.

**Fix:** reword the rule so it can be built. Option (a): within the grace window, issue a **new sibling** token in the same family without revoking it. Option (b): store the successor encrypted (Data Protection) with a 10 s TTL. Option (c): derive the successor deterministically, e.g. `HMAC(server_key, consumed_token)`. Add "consume is atomic (`UPDATE … WHERE consumed_at IS NULL` / xmin concurrency token)". Add an absolute family lifetime (e.g. 90 days) or explicitly accept an unbounded one. State that the access token stays valid for up to 15 minutes after logout.

### F2 — "Standard JwtBearer fetching the JWKS" doesn't work as written [HIGH]

`JwtBearerHandler` discovers keys through `Authority`/`MetadataAddress`, which point to an **OpenID configuration document** (`/.well-known/openid-configuration`) that contains `jwks_uri`. It does not consume a bare `jwks.json`. `RequireHttpsMetadata` defaults to `true`, so `http://auth-api:8080` fails. The JWKS URL also differs by run mode: compose uses `http://auth-api:8080`, IDE mode uses `http://localhost:5001`. Every future service (wishlist-api, cards-bff) hits this, and the spine doesn't say how.

**Fix:** extend AD-6 with one of two approaches. (a) auth-api also serves a minimal `/.well-known/openid-configuration` (`issuer`, `jwks_uri`). (b) ServiceDefaults builds a `ConfigurationManager<JsonWebKeySet>`, or sets `IssuerSigningKeyResolver`, pointing at the JWKS URL. Also fix the config key, e.g. `Jwt__JwksUrl` / `Jwt__Authority`, and set `RequireHttpsMetadata=false` for the internal network only. Separately, AD-14 lists `/.well-known/jwks.json` as auth-api's "public path" and then says the gateway never exposes `/.well-known/*`. Say "internal path" instead.

### F3 — OAuth callback mechanics are undecided and partly wrong [HIGH]

1. ASP.NET remote handlers (`AddGoogle`, `AddDiscord`) own a `CallbackPath` that defaults to `/signin-google` / `/signin-discord`. That path falls outside `/api/auth/`, so AD-14 prefix routing would not send it to auth-api. The handler then signs into a `SignInScheme` (usually an external cookie) and redirects to an app endpoint. So there are two steps: the handler callback, then the app endpoint that issues MyCollection tokens. The spine and stack.md model this as a single `/auth/google/callback`.
2. AD-10 relies on `email_verified` (Google) and `verified` (Discord). Neither the Google handler nor `AspNet.Security.OAuth.Discord` maps these to claims by default. They need `ClaimActions.MapJsonKey(...)`, and Discord needs the `email` scope. If this isn't fixed centrally, the two providers will diverge on the security-critical check.
3. The redirect target after the callback isn't fixed: AD-9 only says "302 to the frontend". Neither is the error path (provider denied, email unverified, refused link), so each provider flow will invent its own. A client-supplied `returnUrl` would be an open redirect.
4. AD-15 depends on `X-Forwarded-Host`, but Nginx `$host` **drops the port**. Behind `localhost:8080`, the generated `redirect_uri` would become `http://localhost/api/auth/google/callback` and break the flow. Use `$http_host`. `ForwardedHeaders.XForwardedHost` must also be enabled explicitly; it's off by default.
5. Minor: `Microsoft.AspNetCore.Authentication.Google` is OAuth2 plus userinfo, not OIDC (no id_token validation). The container diagram labels it "OIDC". Either use the `OpenIdConnect` handler or label it OAuth2.

**Fix:** add to AD-9/AD-10: `CallbackPath = /api/auth/{provider}/callback` (handler-owned) → external cookie → `/api/auth/{provider}/complete` issues tokens, sets the cookie and `302`s to config `Frontend__BaseUrl` + fixed path. Errors `302` to the same URL with `?error=<snake_case code>`. No client-supplied return URL. Require explicit claim mapping for `email_verified`/`verified` in the provider adapters. Require Nginx `proxy_set_header X-Forwarded-Host $http_host;`. Register both redirect URIs (`:8080` and `:5001`) with the providers.

### F4 — `Result<T>` placement contradicts the layering table and AD-13's single mapping [MEDIUM-HIGH]

The paradigm table puts `Result<T>` errors in `<Svc>.Application` and lets Application reference **only Domain**. AD-4/AD-13 put "`Result` → HTTP mapping … in one place (ServiceDefaults)". ServiceDefaults can't map a type that each service defines privately, and it can't know service-specific codes like `email_already_registered`. Each service will therefore build its own `Result` and mapping, which is exactly the divergence AD-13 is meant to prevent.

**Fix:** add `libs/MyCollection.Results` (or `.Primitives`): `Result<T>` plus `Error(code, kind)` with a fixed `ErrorKind` enum (Validation, NotFound, Conflict, Unauthorized, Forbidden). It must have no ASP.NET dependency. Allow Application (and Domain, if wanted) to reference it, and update the table and AD-3's rules. ServiceDefaults maps `ErrorKind` → status, and `code` passes through unchanged.

### F5 — AD-12 migration bundle has no image target and unstated build prerequisites [MEDIUM]

AD-19 says each Dockerfile has only `dev` and `final` targets, but AD-12's `<svc>-migrate` needs an image that contains the bundle. That's a contradiction. Building a bundle also needs:

- a pinned `dotnet-ef` (local tool manifest `.config/dotnet-tools.json`)
- a design-time `DbContext` path that doesn't depend on runtime secrets. `IDesignTimeDbContextFactory` in Infrastructure keeps AD-17's fail-fast startup out of the way.
- a runtime identifier matching the base image (`-r linux-musl-x64 --self-contained` for Alpine)
- the connection string passed via `--connection` or an env var

In dev, a new migration means rebuilding `auth-migrate` (`docker compose up --build auth-migrate`), which is fine but should be said.

**Fix:** AD-19 → "targets `dev`, `migrate`, `final`". Add the tool manifest, the design-time factory and the RID to AD-12, and document the dev migration loop.

### F6 — Nginx static upstream resolution breaks "addable without restructuring" and restarts [MEDIUM]

A `proxy_pass http://auth-api:8080` with a static hostname is resolved once at Nginx startup. The gateway won't start if a routed service isn't running, which matters because later slices add `wishlist-api` and `cards-bff` routes and devs run subsets. It also keeps a stale IP after a service container is recreated, which returns 502 after `docker compose up --build auth-api`.

**Fix:** add to AD-14 either `upstream { zone …; server auth-api:8080 resolve; }` with `resolver 127.0.0.11 valid=10s;` (Nginx OSS ≥ 1.27.3, and 1.30 qualifies) or variable-based `proxy_pass` with a resolver. Also fix Nginx CORS specifics that otherwise vary by route:

- `map $http_origin` against the allow-list
- `Vary: Origin`
- `add_header … always` so 401/429/5xx carry CORS headers
- a preflight `OPTIONS` → 204
- `limit_req_status 429` (the default is 503)
- services never emit CORS headers

### F7 — CAP-8 hot reload on the author's Windows host is at risk; the Docker build context is unstated [MEDIUM]

The owner develops on Windows. With bind mounts from an NTFS path into Linux containers, inotify events don't propagate, so `dotnet watch` won't notice saves unless `DOTNET_USE_POLLING_FILE_WATCHER=1` is set. Mounting host `bin/`/`obj/` also mixes Windows and Linux restore assets. CPM, `global.json`, `Directory.*.props` and `libs/` live at the repo root, so every service Dockerfile needs the **repo root** as its build context and the source mount must cover the root.

**Fix:** add to AD-19: build context = repo root; the dev target sets `DOTNET_USE_POLLING_FILE_WATCHER=1`; anonymous volumes (or per-container `BaseIntermediateOutputPath`) for `bin/` and `obj/`; the mount covers `apps/<svc>` + `libs/` + root props.

### F8 — Email identity normalization undecided [MEDIUM]

AD-10 and CAP-1 depend on `users.email` uniqueness and on email matching for linking. Case and whitespace handling isn't decided. For example, `Foo@x.com` from Google against `foo@x.com` from Discord would silently fail to link or create duplicates. The lookup order is also unstated: `(provider, provider_user_id)` first, then email. So is what happens when a provider email changes.

**Fix:** AD-10: store a normalized email (trimmed and lower-cased, or a `citext` column) with a unique index on it. Social login resolves by `(provider, provider_user_id)` first and by email only for first-time linking. `user_oauth` has a unique `(provider, provider_user_id)`.

### F9 — Operational envelope: dev and environment decisions missing from the spine [MEDIUM]

These live in the memlog or nowhere, so the spine doesn't bind them:

- **HTTP in dev / TLS deferred to prod.** This is in the memlog but absent from the spine. AD-9 mandates `Secure` cookies. Chrome and Firefox accept `Secure` on `http://localhost`, but other clients may not: some HTTP tooling (Postman's cookie jar, older curl versions, Safari) won't send or store them over HTTP. The success-signal script and Postman runs then fail at refresh or logout. State the decision and the supported dev clients, or make `Secure` config-driven for Development only.
- **SameSite=Strict implies a same-site web UI.** The refresh cookie is only sent if the web UI is **same-site** with the gateway (same registrable domain and scheme; ports don't matter, so `localhost:5173` → `localhost:8080` works). A future UI on a different domain would silently lose refresh. Record this as a constraint on the Deferred web UI and production hosting.
- **Environment matrix.** Specify `ASPNETCORE_ENVIRONMENT` per compose file (base = Production, override = Development?), which in turn controls Scalar and Development settings.
- **PostgreSQL 18 image volume path.** The memlog records the change to `/var/lib/postgresql`. The spine doesn't, and a story copying the classic `/var/lib/postgresql/data` mount will break on PG 18.
- **Container health probe.** The `final` image is ASP.NET runtime (possibly chiseled) and has no `curl`/`wget`, so compose `healthcheck` needs a decided probe: a tiny probe binary, a `--healthcheck` app mode, or a non-chiseled base.
- **Data Protection keys.** OAuth `state`/correlation and external cookies are encrypted with Data Protection. With ephemeral keys, an in-flight OAuth login fails after a container or `dotnet watch` restart, and any multi-replica setup breaks. Decide on dev (volume-persisted key ring) and list the prod store under Deferred.
- **Forwarded headers "trusting only the gateway's network".** This needs a **pinned compose network subnet** (or a config value), because Docker assigns subnets dynamically. In .NET 10 the option is `KnownIPNetworks`. Also decide the IDE-mode behavior, where there's no gateway.

**Fix:** extend AD-19 or add an "AD-22 Environments" with these items, and move true unknowns into a new **Open Questions** section.

### F10 — Spec success signal for CAP-5/6 is unverifiable under the spine's test decisions [MEDIUM]

The SPEC's success signal is a scripted run through the gateway, including Google and Discord logins. The spine defers E2E and the mock OAuth server, and integration tests fake the providers. So nothing in this slice demonstrates CAP-5/6 end to end. The memlog says the success signal "must be reworded", but the spine doesn't record this as an open item.

**Fix:** add an Open Question or dependency: "SPEC success signal to be reworded: scripted password flows through the gateway, plus manual Google/Discord verification checklist", or pull a mock OAuth2 server into this slice for the scripted run.

### F11 — AD-16 "committed OpenAPI" isn't enforced; AD-21 affected inputs incomplete [MEDIUM-LOW]

Nothing fails CI when the committed `apps/<svc>/openapi/<svc>.json` is stale. `Microsoft.Extensions.ApiDescription.Server` also defaults to `obj/` and to a `<ProjectName>.json` file name, so the spine's path needs `OpenApiDocumentsDirectory` and file-name settings. For AD-21: `nx affected` in GitHub Actions needs full history (`fetch-depth: 0` plus `nrwl/nx-set-shas`). Changes to `Directory.Packages.props`, `Directory.Build.props` or `global.json` must mark every .NET project affected; otherwise a package bump skips build and test.

**Fix:** AD-16: add a CI step, `git diff --exit-code apps/*/openapi`. AD-21: add `namedInputs.sharedGlobals` covering the root props and `global.json`, full-history checkout, and a gateway `nginx -t` target so the gateway participates in `affected`. Optionally add a compose smoke test.

### F12 — `@nx/dotnet` is experimental and not treated as a risk [LOW]

The spine pins `@nx/dotnet` 23.2.1 (experimental) but doesn't say what happens if target inference (`build`, `test`) is inadequate. The memlog also flags ArchUnitNET.xUnitV3 compatibility with xunit.v3 4.x as "to confirm at scaffold". Both are open questions, not decisions.

**Fix:** add them to Open Questions with a fallback, e.g. explicit `project.json` targets wrapping `dotnet build`/`dotnet test` if inference falls short, and pinning xunit.v3 3.x if ArchUnitNET is incompatible.

### F13 — Observability scope silently widened versus the SPEC [LOW]

The SPEC lists health checks under non-goals (phase 6). AD-19 makes them mandatory and relies on them for `service_healthy` ordering. That's a reasonable choice, but it should be an explicit, acknowledged deviation so the spec refresh picks it up.

**Fix:** note it in the spine (e.g. under AD-19, "supersedes SPEC non-goal for health checks") and in the spec refresh list.

### F14 — AD-3 "except in composition-root registration" needs a concrete rule [LOW]

ArchUnitNET works on types, not statements, so "Api may touch Infrastructure only for registration" has to be expressed as type rules.

**Fix:** Infrastructure exposes one `AddInfrastructure(this IServiceCollection, IConfiguration)` extension (and `AddXxxAuthentication` for the OAuth handlers). The rule then reads: "types in `<Svc>.Api` other than `Program` depend on no Infrastructure type except `*ServiceCollectionExtensions`." Relatedly, the capability map says "`Features/Register`", a vertical-slice term. Fix a folder convention inside Application, e.g. `UseCases/<Feature>/`.

### F15 — A Deferred item hides a cross-unit auth decision [LOW]

"How wishlist-api and cards-bff call each other" is deferred to their slices. It implies service-to-service authentication: forwarding the user's JWT under `aud=mycollection`, or client-credentials tokens that auth-api would have to issue. The first option is already implicitly allowed by AD-7's single audience. The second changes auth-api, which is being built now.

**Fix:** decide now that user-context calls forward the caller's access token and reuse the single audience. Keep only the transport and resilience details deferred, and name one resilience library (`Microsoft.Extensions.Http.Resilience`) so the two later slices don't diverge.

## Minor notes

- CAP-2's success says login "returns both tokens". Under AD-9 the refresh token comes as a cookie, which should be reflected in the spec refresh. CAP-1 doesn't say whether register also signs the user in (tokens and cookie) or returns 201 only, and whether `name` is required. JWT `name` is mandatory in AD-7.
- AD-4 cites "JWT validation setup (AD-7)"; AD-6 is the main source.
- AD-18: `traceId` differs per service, because Nginx emits no `traceparent`. That's fine, since `requestId` is the correlator, but say so to avoid confusion.
- The Stack table versions were not independently re-verified by this review. They are taken as recorded in the memlog.

## Coverage map check

| CAP | Covered by | Gap |
|---|---|---|
| CAP-1 | AD-2, 10, 11, 13 | Email normalization (F8); register response shape |
| CAP-2 | AD-6–10 | — |
| CAP-3 | AD-8, 9 | Grace window not implementable (F1) |
| CAP-4 | AD-8, 9 | Access-token residual validity unstated (F1) |
| CAP-5 | AD-9, 10, 15 | Callback path, claim mapping, redirect/error target, `$http_host` (F3); verification (F10) |
| CAP-6 | same | same |
| CAP-7 | AD-1, 14, 15, 18 | Upstream resolution, CORS details, `limit_req_status` (F6) |
| CAP-8 | AD-12, 17, 19 | Migrate target (F5), Windows hot reload and build context (F7), env details (F9) |
