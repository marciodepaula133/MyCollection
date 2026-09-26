---
reviews: ../ARCHITECTURE-SPINE.md
lens: adversarial (pairs of units that obey every AD yet build incompatibly)
date: 2026-09-26
verdict: 'Not yet build-safe. The paradigm and topology are right, but the first slice has one internally contradictory rule (AD-8), and at least five places where two compliant implementers will produce parts that do not fit together.'
---

# Adversarial review: MyCollection architecture spine

**Verdict:** The topology, layering and token model are sound. The spine still falls short as a build substrate. AD-8 cannot be built as written. The OAuth callback path, the `Result`/error-mapping location, the gateway's nginx mechanics, the migration and OpenAPI build steps, and JWKS consumption are each open to two readings that are both literally compliant but do not fit together. Most of these problems hit the first slice (auth-api plus gateway).

Every finding follows the same format. **Pair** names the two units that both obey the spine but clash. **Clash** says what breaks. **Rule** is the proposed new or tightened AD text. Severity: **Critical** means it cannot be built as written. **High** means the first slice will break or silently drift. **Medium** affects the next slices or will cause rework. **Low** is polish.

---

## H1. Critical: the AD-8 "same successor" requirement conflicts with hash-only storage, and consumption is not atomic

- **Pair:** Story "RefreshToken use case" (dev A) vs story "refresh_tokens persistence adapter" (dev B).
- **Clash:**
  1. AD-8 says a consumed token presented again within 10 s "returns the same successor". The same rule says tokens are stored *only* as a SHA-256 hash, so the raw successor value can't be recovered. Dev A stores the raw successor in a side column to satisfy "same successor", which breaks "only as a hash". Dev B issues a new token and breaks "same successor". Both can cite AD-8.
  2. The table has no link from a token to its successor (`replaced_by_id`), so "the successor" can't even be found.
  3. Two tabs refreshing at the same moment both read `consumed_at IS NULL`. Both then consume the token and create two successors, and nothing in the spine says this must be atomic. A "read, then check `consumed_at`, then write" implementation passes every unit test and still forks the family. It can also make the second request compute "reuse after 10 s" from a stale read.
  4. The 10 s window is measured against `consumed_at`. Dev A stamps it with `DateTime.UtcNow`/`TimeProvider`, and dev B uses Postgres `now()` as a default. Clock skew between the app and the database then shifts the theft threshold.
- **Rule (replace the AD-8 rule text):**
  > Access token lifetime is 15 min. A refresh token is 32 bytes of CSPRNG output, base64url-encoded, stored only as its SHA-256 hash in `refresh_tokens (id, user_id, family_id, token_hash UNIQUE, replaced_by_id NULL, expires_at, consumed_at, revoked_at, created_at)`. Refresh expiry slides to now + 30 days on each rotation. **A family also has an absolute cap of 90 days from its first token.** Consumption is a single conditional statement: `UPDATE refresh_tokens SET consumed_at=@now, replaced_by_id=@newId WHERE token_hash=@h AND consumed_at IS NULL AND revoked_at IS NULL AND expires_at>@now`, run in the same transaction as the successor's insert. If exactly one row changes, the refresh succeeds. If zero rows change, reload the row: if it was consumed ≤ 10 s ago and the family is not revoked, **issue a new sibling token in the same family** (raw token values are never stored or re-sent) plus a fresh access token. Any other case revokes the family (`UPDATE … SET revoked_at=@now WHERE family_id=@f AND revoked_at IS NULL`) and returns `401 refresh_token_reused`. All timestamps come from the injected `TimeProvider`, and there are no database-side defaults for business timestamps. Logout revokes the family and is idempotent.

## H2. High: the OAuth callback flow is under-specified, so the Google and Discord stories will build different machinery

- **Pair:** Story "Google sign-in" (dev A) vs story "Discord sign-in" (dev B). Also auth-api vs gateway, and auth-api vs the Google/Discord console registration.
- **Clash:**
  1. **Mechanism.** Dev A uses `AddGoogle()`, where the ASP.NET remote handler *owns* `CallbackPath`, needs a `SignInScheme` (a temporary external cookie), and then redirects to a second endpoint of your own. Dev B writes a manual code-exchange adapter in Infrastructure and puts the logic directly in `/callback`. Both fit "OAuth adapter (Infrastructure) + callback endpoint". Neither matches the other's routes, cookies or tests.
  2. **External cookie SameSite.** The callback is reached by a cross-site top-level navigation from Google. If the external/correlation cookie is `SameSite=Strict`, copying AD-9's style "for consistency", it is dropped and the login fails with "Correlation failed".
  3. **Verified email.** AD-10 depends on "provider-asserted verified email". Google's handler exposes it differently from Discord (Discord's `verified` is not mapped to a claim by default). Each dev builds a different shape to hand to Application, or skips the check.
  4. **Where the 302 goes.** AD-9 says "302 to the frontend" but names no config key, path or error channel. One dev reads `returnUrl` from the query string, which is an open redirect. Another hard-codes `http://localhost:4200`.
  5. **Failures.** AD-13 says every 4xx is ProblemDetails. A refused unverified email arrives on a browser navigation, where a JSON 409 body strands the user on a raw JSON page.
  6. **`redirect_uri` derivation (with AD-15).** The idiomatic nginx line `proxy_set_header X-Forwarded-Host $host;` strips the port, so auth-api builds `http://localhost/api/auth/google/callback`. That URI is unregistered and Google rejects it. In direct/IDE mode (port 5001) there is no gateway, so the URI becomes `http://localhost:5001/...`. In compose, "trust only the gateway's network" needs a known CIDR, but compose assigns subnets dynamically. Hard-coding `172.18.0.0/16` in ServiceDefaults makes the forwarded headers get silently ignored, and the URI becomes `http://auth-api:8080/...`.
- **Rule (new AD-22, "OAuth sign-in flow", and tighten AD-15):**
  > auth-api uses the ASP.NET remote authentication handlers (`AddGoogle`, `AddDiscord`) for every provider, registered in `Auth.Api`'s composition root through an Infrastructure extension method. Per provider `{p}`: `GET /api/auth/{p}` issues the challenge. `CallbackPath` is `/api/auth/{p}/callback`, which is the only URI registered at the provider. The handler signs into a scheme `External`: a cookie `ext_auth` with `HttpOnly; Secure; SameSite=Lax; Path=/api/auth; Max-Age=300`. Its `RedirectUri` is `/api/auth/{p}/complete`, a thin endpoint that reads the `External` principal, maps it to `ExternalIdentity(provider, providerUserId, email, emailVerified, displayName)` (a record declared in Auth.Application), calls one use case, deletes `ext_auth`, sets `refresh_token` on success, and `302`s to `Frontend__AuthCallbackUrl` on success or `Frontend__AuthCallbackUrl?error=<code>` on failure. **Redirect targets come only from configuration. No query-supplied return URLs are accepted.** Each provider adapter must fill `emailVerified` from the provider's own claim (Google `email_verified`, Discord `verified`, mapped explicitly in `ClaimActions`). A missing claim counts as `false`. Browser-navigation endpoints (`/{p}`, `/{p}/callback`, `/{p}/complete`) are the only AD-13 exception: they signal errors by redirect, never by a JSON body.
  >
  > **AD-15 addition:** the gateway sets `X-Forwarded-Host $http_host` (keeping the port), `X-Forwarded-Proto $scheme` and `X-Forwarded-For $proxy_add_x_forwarded_for`, and **overwrites** client-supplied values. Compose declares the internal network with a fixed subnet (`internal: 172.30.0.0/24`). Services read the trusted range from `ForwardedHeaders__KnownNetworks` and keep `ForwardLimit=1`. Provider consoles register both `http://localhost:8080/api/auth/{p}/callback` (gateway) and `http://localhost:5001/api/auth/{p}/callback` (direct dev).

## H3. High: `Result<T>` has no legal home, and the "one place" status mapping forces domain codes into a shared lib

- **Pair:** The `libs/MyCollection.ServiceDefaults` author vs the auth-api Application author. Later, auth-api vs wishlist-api.
- **Clash:**
  1. AD-4 puts "ProblemDetails/`Result` → HTTP mapping" in libs, and AD-4's own table places ServiceDefaults on ASP.NET. If `Result<T>` lives in ServiceDefaults, `Auth.Application` must reference an ASP.NET-dependent assembly, and AD-3 (Application doesn't depend on ASP.NET Core) fails. If each service defines its own `Result<T>`, ServiceDefaults can't map it, so AD-13's "one place" fails.
  2. "Error → status mapping lives in one place (ServiceDefaults)": the lib author adds `invalid_credentials → 401` and `email_already_registered → 409` to the lib. Every new error code in any service is then a lib change that forces a rebuild of every service, which is the coupling AD-4 exists to prevent. The alternative, a mapping in each service, contradicts "one place".
  3. Concurrent registrations with the same email both pass the "exists?" check. The unique index then throws `DbUpdateException`, which surfaces as a 500 even though AD-13 says to throw only for bugs.
  4. `Foo@X.com` (password) vs `foo@x.com` (Google) either duplicates users or defeats AD-10 linking. Nothing says emails are normalised.
  5. Register writes `users` plus `user_credentials`. If one repository calls `SaveChanges` and the other does too, a crash between them leaves a password-less user that blocks the email forever.
- **Rule (tighten AD-4 and AD-13, add to AD-11):**
  > `libs/MyCollection.Results` is framework-free (no package references). It defines `Result`, `Result<T>` and `Error(string Code, ErrorKind Kind, string Message)` with `ErrorKind ∈ {Validation, Unauthorized, Forbidden, NotFound, Conflict, RateLimited, Unavailable}`. Application projects may reference it, and ArchitectureTests allow it. ServiceDefaults maps **`ErrorKind` → HTTP status** (400/401/403/404/409/429/503) and writes the ProblemDetails with `code = Error.Code`. **Error codes are owned and declared by each service's Application layer.** No service-specific code ever appears in `libs/`.
  >
  > **AD-11 addition:** one use case is one transaction. Application declares `IUnitOfWork`. Repositories never call `SaveChanges`. Infrastructure translates unique-constraint violations (Postgres `23505`) into `Conflict` results in the adapter, never into exceptions. Emails are trimmed and lower-cased (invariant) before storage and comparison, and `users.email` is unique on the normalised value.

## H4. High: nginx mechanics make "CORS, 429, request logging" fail in ways that pass code review

- **Pair:** The gateway story (nginx.conf) vs the auth-api story (and later the web UI).
- **Clash:**
  1. **CORS on errors.** `add_header Access-Control-Allow-Origin …` without `always` is dropped on 4xx/5xx. auth-api's `401 refresh_token_reused` then reaches the browser as an opaque CORS failure, and the web UI's silent-refresh logic can't tell "logged out" from "network down".
  2. **Preflight.** If nginx forwards `OPTIONS`, Minimal API answers `405` and every credentialed `POST` fails its preflight. If an auth-api dev "helpfully" adds `UseCors` for direct-port testing, responses via the gateway carry two `Access-Control-Allow-Origin` headers, which browsers reject.
  3. **Allow-list "from env".** nginx doesn't read env vars. One dev uses the `nginx:1.30-alpine` `/etc/nginx/templates` envsubst feature. Another writes a `map` by hand. A third reflects `$http_origin` unconditionally, which is compliant-sounding and insecure.
  4. **429.** `limit_req` returns **503** unless `limit_req_status 429;` is set. The literal `rate=5r/m` with no burst lets through one request every 12 s, so a second login attempt within 12 s gets rejected.
  5. **ProblemDetails.** AD-13 says *every* 4xx/5xx body is ProblemDetails, but nginx-generated 429/502/504 responses are HTML.
  6. **Upstream DNS.** A static `proxy_pass http://auth-api:8080` resolves once at startup. `docker compose up --build auth-api` gives the container a new IP, and the gateway returns 502 until it restarts. When wishlist-api is added, nginx refuses to start at all if that container isn't up ("host not found in upstream").
  7. **X-Request-Id.** Dev A passes the client's header through (spoofable correlation). Dev B always generates one. In direct mode no id exists, so services log `requestId=null`.
- **Rule (tighten AD-14):**
  > The gateway config is `apps/gateway/templates/default.conf.template`, rendered by the image's envsubst from `GATEWAY_CORS_ORIGINS` into a `map $http_origin $cors_origin` (exact matches only, default empty). The gateway answers every `OPTIONS` preflight itself with `204` and never forwards it. CORS headers (`Access-Control-Allow-Origin $cors_origin`, `Allow-Credentials true`, `Vary: Origin`) are added with `always`. **Services never register CORS middleware.** Requests with an `Origin` header outside the allow-list get `403 origin_not_allowed`. Limits: `limit_req_status 429`. Global zone `10r/s burst=20 nodelay`. Login/register zone `5r/m burst=5 nodelay`. Gateway-generated 403/404/413/429/502/503/504 responses return static `application/problem+json` bodies with codes `origin_not_allowed`, `not_found`, `payload_too_large`, `rate_limited` and `upstream_unavailable`. Upstreams are proxied through variables with `resolver 127.0.0.11 valid=10s`, so the gateway starts and recovers independently of any service. The gateway always overwrites `X-Request-Id` with `$request_id`, and ServiceDefaults generates one when the header is absent (direct mode). Gateway config is covered by a CI smoke test (`nginx -t` plus a compose-based check of CORS, preflight and 429).

## H5. High: the migration bundle and OpenAPI generation both boot `Program.cs` and fail against AD-17 fail-fast

- **Pair:** The "auth-api config/options" story (AD-17 fail-fast) vs the "compose + migrate" story (AD-12) vs the "OpenAPI contract" story (AD-16).
- **Clash:**
  1. By default an EF bundle and build-time OpenAPI generation (`Microsoft.Extensions.ApiDescription.Server`) both build the app's host from `Program.cs`. With `ValidateOnStart`, the migrate container fails because it has no `Jwt__SigningKeyPath` and no `Google__ClientSecret`. OpenAPI generation fails in CI and in `docker build` for the same reason. Each story passes on its own and they fail together.
  2. AD-19 says Dockerfiles have only `dev` and `final` targets, so the bundle has no image to run from. One dev adds a third stage. Another runs `dotnet ef database update` from an SDK container, which is not a bundle and breaks AD-12.
  3. "Apps never need DDL rights" has no mechanism, because compose gives both containers `POSTGRES_USER`.
  4. The default generated file is `obj/Auth.Api.json`, while AD-16 says `apps/auth-api/openapi/auth-api.json`. Under the `dev` target (`dotnet watch` plus source mount), "each build" rewrites a committed file continuously. With no CI check, the committed file drifts. "Committed" is only a contract if CI fails on a diff.
  5. Dev `dotnet watch` hot-reloads a new migration's model while the database stays unmigrated. `/health/ready` still says healthy.
- **Rule (tighten AD-12, AD-16 and AD-19):**
  > **AD-12:** each Infrastructure project has an `IDesignTimeDbContextFactory` that reads only `ConnectionStrings__Postgres`. Bundles never build the app host. Each service Dockerfile has three targets: `dev`, `migrate` (the `efbundle` plus the runtime-deps base) and `final`. The `<svc>-migrate` service runs `efbundle --connection "$ConnectionStrings__PostgresMigrator"` as the database owner role. The app connects as `<svc>_app`, which has DML-only grants created by `apps/<svc>/db/init.sql` (including `ALTER DEFAULT PRIVILEGES`). `/health/ready` reports unhealthy while `GetPendingMigrationsAsync()` is non-empty. In dev, after adding a migration, run `docker compose run --rm <svc>-migrate`.
  >
  > **AD-16:** OpenAPI is generated by the Nx target `openapi` (not every build) into `apps/<svc>/openapi/<svc>.json` (`OpenApiDocumentsDirectory`, `--file-name <svc>`). `Program.cs` skips options validation and external I/O when running under the document generator (`GetDocument.Insider`). CI runs `nx affected -t openapi` then `git diff --exit-code apps/*/openapi`. Health and `.well-known` endpoints are excluded from the document.

## H6. High (cross-service): "validate locally via JWKS" has two incompatible JwtBearer setups, and claim mapping hides `sub`

- **Pair:** auth-api (issuer, publishes only `/.well-known/jwks.json`) vs the ServiceDefaults author, vs the wishlist-api dev who reads the user id.
- **Clash:**
  1. The textbook JwtBearer setup is `Authority = "http://auth-api:8080"`. That fetches `/.well-known/openid-configuration`, which auth-api doesn't publish. It also requires HTTPS metadata by default and expects the discovery `issuer` to match. `mycollection-auth` is not a URL. Validation fails at the first request.
  2. `MapInboundClaims` still defaults to `true`, so `sub` arrives as `ClaimTypes.NameIdentifier`. Dev A's `User.FindFirst("sub")` returns null, while dev B's `NameIdentifier` works, so two services disagree on who the user is. `Guid.ToString()` casing and format vary too (`D` vs `N`), and wishlist-api stores `sub` as a string key.
  3. `kid` has no defined derivation. If it is random per process, two auth-api replicas publish different JWKS, and tokens from one fail against a cached set from the other.
  4. No env key names the JWKS location for validators, and it's unclear whether auth-api validates its own tokens by an HTTP call to itself.
  5. When auth-api is down, validators return 500 (IDX errors) or 401 (a misleading "log in again"), depending on the dev.
- **Rule (tighten AD-6 and AD-7):**
  > auth-api publishes both `/.well-known/jwks.json` and a minimal `/.well-known/openid-configuration` (`{"issuer":"mycollection-auth","jwks_uri":"<Jwt__PublicJwksUrl>"}`). ServiceDefaults' `AddMyCollectionJwt()` is the only JwtBearer setup: `MetadataAddress = Jwt__MetadataUrl` (internal, e.g. `http://auth-api:8080/.well-known/openid-configuration`), `RequireHttpsMetadata = false` only for internal-network URLs, `MapInboundClaims = false`, `ValidIssuer = "mycollection-auth"` and `ValidAudience = "mycollection"` as **code constants, not config**, `ClockSkew = 30 s`. It exposes `ICurrentUser.Id` (parsed from `sub`). Endpoints and use cases get the user only from `ICurrentUser`. `sub` is the lower-case hyphenated UUID (`Guid.ToString("D")`), and services store it in that exact form. `kid` = RFC 7638 JWK thumbprint of the public key. auth-api validates its own access tokens in-process from the same key (no self HTTP call). A failure to fetch metadata or keys yields `503 upstream_unavailable`, never 401. A service's `/health/ready` never depends on auth-api.

---

## Further holes (Medium / Low)

### H7. Medium: SameSite=Strict silently constrains where the web UI may be hosted
- **Pair:** auth-api (AD-9 cookie) vs the future `apps/web` deployment.
- **Clash:** `SameSite=Strict` means the refresh cookie is sent only if the web UI and the gateway are *same-site* (same registrable domain). If the web UI is hosted on a different domain (a typical `*.vercel.app` or `*.pages.dev` setup), every silent refresh after the first navigation loses the cookie. Dev works by accident because `localhost:4200` and `localhost:8080` are same-site.
- **Rule (AD-9 addition):** "The web UI is served same-site as the gateway, preferably same-origin (the gateway serves `apps/web` at `/` in production-shaped compose). Clients call the API with `credentials: 'include'`. A cross-site frontend requires a new AD."

### H8. Medium: auth response shapes differ between the login, refresh, register and logout stories
- **Pair:** Login story vs refresh story vs register story (vs the web UI's generated client).
- **Clash:** Login returns `{accessToken, expiresIn}`, refresh returns `{token, expiresAt}`, and register may or may not log in. Logout requires a valid access token in one version, so a user whose token expired can't log out. The logout cookie-clear uses the wrong `Path`, which leaves the cookie alive. Password login for an unknown email skips bcrypt, so the timing difference reveals which emails are registered (against CAP-2).
- **Rule (AD-9 addition):** "Login, refresh and every OAuth completion share one `TokenResponse { accessToken, tokenType: \"Bearer\", expiresAt }`. Register returns `201` with `{ id, email }` and issues no tokens. Logout is authenticated only by the refresh cookie, is idempotent (`204` even without a cookie) and clears the cookie with identical `Path`/`Secure`/`SameSite` attributes. Any `401` from refresh also clears the cookie. Login always performs one bcrypt verification (against a fixed dummy hash when the user or credential is missing)."

### H9. Medium (next slices): service-to-service calls and the meaning of "BFF" are undefined
- **Pair:** cards-bff (`GET /api/cards/wishlist-summary`) vs wishlist-api. Also cards-bff vs AD-9.
- **Clash:** One dev calls wishlist-api *through the gateway*. All users then share the BFF container's IP for the 10 r/s per-IP limit, and the call re-enters CORS. Another calls it directly with no token, and wishlist-api returns 401. A third invents a service credential that auth-api doesn't issue. Separately, "BFF" commonly means *token-handling BFF* (server-side session cookie), which contradicts AD-9's in-memory access token.
- **Rule (AD-1 addition):** "Service-to-service calls go directly over the internal network to `Services__<Target>__BaseUrl` (never via the gateway), forward the caller's `Authorization` header and `X-Request-Id` unchanged, and use a timeout. Client-credential service tokens are out of scope until a call has no end user. cards-bff is an aggregation BFF and a protected resource like any other; it never holds user tokens or sessions."

### H10. Medium (next slices): AD-11/AD-12/AD-19 are Postgres-shaped, and Mongo and Redis services get no rules
- **Pair:** wishlist-api (Mongo) vs AD-11/AD-12. Also cards-bff (Redis) vs AD-19's readiness check.
- **Clash:** AD-12 binds only relational databases, so wishlist-api creates indexes from app startup (the pattern AD-12 forbids) or never creates them. The Mongo driver's `Guid` representation must be configured explicitly or it throws or serialises inconsistently. cards-bff's `/health/ready` "database check" on Redis makes the BFF unready exactly when it should serve stale data (roadmap: fall back to stale cache).
- **Rule:** "Non-relational stores follow AD-12 in spirit: schema and index setup runs in `<svc>-migrate` (a one-shot console command in the same image), never at app start. Mongo `Guid`s use `GuidRepresentation.Standard`, and ids are UUIDv7 as in AD-11. `/health/ready` checks only dependencies without which the service cannot answer any request. A cache (Redis) is not one."

### H11. Medium: container health probes and gateway startup ordering
- **Pair:** The Dockerfile `final` target author vs the compose healthcheck author. Also gateway vs later services.
- **Clash:** `final` on `aspnet:10.0-*-chiseled` has no shell or curl, so `healthcheck: curl -f …` never passes and the gateway never starts. With the gateway declared `depends_on: service_healthy` on *every* service, one failing slice takes down the whole entry point.
- **Rule (AD-19 addition):** "`final` uses `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` and the healthcheck executes the app's own probe (`["CMD","dotnet","Auth.Api.dll","--healthcheck"]` or a bundled static probe binary). No curl or wget is assumed. The gateway depends on services with `service_started` only (H4's resolver handles availability)."

### H12. Low: small divergence points
- The **refresh-token cookie** is `Secure`. Postman over `http://localhost:5001` may not send Secure cookies back, so direct-mode testing of refresh and logout fails. State that direct-mode refresh is tested via the gateway or via integration tests.
- **Profile ownership:** `name`/`email` in the JWT can be stale for up to 15 min. State that auth-api is the sole owner of the user profile, that other services never persist `name`/`email` from claims, and that user-deletion propagation is deferred.
- **Access-token `email` claim** plus AD-18 "never log full emails": JwtBearer/Serilog request logging of claims must be disabled explicitly (ServiceDefaults enricher allow-list).
- **SPEC drift:** the SPEC still says `docker compose -f docker-compose.dev.yml up`, while AD-19 says `docker compose up`. Refresh the SPEC's CAP-8 and success signal so story acceptance criteria don't follow the stale command.
- **CI (AD-21):** `nx affected` needs `fetch-depth: 0` and base/head SHAs, and Testcontainers needs Docker on the runner. Name `ubuntu-latest` and the `nrwl/nx-set-shas` action.

---

## Priority for the first slice

| # | Hole | Severity | Bites |
|---|---|---|---|
| H1 | Refresh grace "same successor" vs hash-only storage; non-atomic consume | Critical | auth-api refresh story |
| H2 | OAuth flow mechanism, external cookie, verified-email shape, redirect target, `$host` port loss, KnownNetworks | High | Google/Discord stories, gateway |
| H3 | `Result<T>` location vs AD-3; lib-level code→status mapping; unique race; email normalisation; unit of work | High | every auth-api use case |
| H4 | nginx CORS `always`/preflight/env templating, 503-vs-429, burst, HTML errors, DNS caching, request id | High | gateway story |
| H5 | Bundle and OpenAPI generation boot Program.cs vs fail-fast; no `migrate` target; DDL roles; OpenAPI drift | High | compose, CI |
| H6 | JWKS discovery, `MapInboundClaims`, `sub` format, `kid` derivation, 503 vs 401 | High | ServiceDefaults now, wishlist/cards next |
| H7–H11 | Same-site UI hosting, response shapes, service-to-service calls, non-relational stores, health probes | Medium | web UI, next slices |
