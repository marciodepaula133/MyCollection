---
title: 'CAP-2: Log in with email and password'
type: 'feature'
created: '2026-10-14'
status: 'done'
route: 'dispatch'
baseline_commit: '43f0804ece18b8b5d8dbd3c5a5b3fe993e4fd199'
review_loop_iteration: 0
context: ['{project-root}/docs/auth.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A registered user has no way to sign in. auth-api issues no tokens: there is no signing key loading, no JWT issuer and no `refresh_tokens` table.

**Approach:** Add `POST /api/auth/login`, which takes a JSON body `{ email, password }`. The `Login` use case looks up the normalized email and checks the password with bcrypt. It then issues an RS256 access token (15 min) and starts a new refresh-token family (opaque, SHA-256-hashed, 30 days, 90-day family cap). The response is `200 TokenResponse { accessToken, expiresAt }` plus the `refresh_token` cookie.

## Boundaries & Constraints

**Always:**
- AD-6/7: RS256 with the PEM at `Jwt:SigningKeyPath`; header `kid` is the RFC 7638 thumbprint; claims `iss=mycollection-auth`, `aud=mycollection`, `sub` (lower-case `D` Guid), `name` (`"{First} {Last}"`, trimmed; always present for password users, since register requires both names; the fallback for users without names is decided in CAP-5), `email`, `iat`, `exp`, `jti`. Issuer and audience are code constants.
- AD-8: raw refresh token ≥ 256 bits from a CSPRNG, base64url; only its SHA-256 hex is stored. Login inserts one row with a new `family_id`, `expires_at = now+30d`, `family_expires_at = now+90d`. Schema: `refresh_tokens(id, user_id, family_id, token_hash UNIQUE, replaced_by_id, family_expires_at, expires_at, consumed_at, revoked_at)` plus an index on `family_id`; `user_id` FK to users with cascade delete; `replaced_by_id` is a nullable self-FK with `ON DELETE SET NULL`. The columns used by CAP-3/4 are created now and left null.
- AD-9: cookie `refresh_token`: `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`, `Expires` = the token's `expires_at`. The refresh token never appears in the body or the URL.
- Every authentication failure returns the same `401 invalid_credentials` with the same message (unknown email, wrong password, user without a password credential). An unknown email, or a user without a password credential, returns immediately without a bcrypt check, as ASP.NET Core Identity's `PasswordSignInAsync` does (decided 2026-10-15).
- JSON is the request format for login and every future endpoint (decided 2026-10-14). Register's multipart form is the documented exception, kept only for its file upload.
- Request shape: `LoginRequest` is a JSON record with `[Required, EmailAddress]` email and `[Required, MaxLength(1024)]` password. There is no minimum length at login. A request that fails these checks returns `400 validation_failed`.
- Every endpoint, in every service, answers a body the framework rejects before validation with ProblemDetails carrying `code` and `traceId` (decided 2026-10-14). That covers malformed or empty JSON (`400 malformed_request`) and an unsupported content type (`415 unsupported_media_type`). This lives once in `MyCollection.ServiceDefaults`, using the framework's own ProblemDetails pipeline (`AddProblemDetails` + `UseStatusCodePages`), not hand-written parsing.
- The use case owns every rule; Infrastructure only loads data, hashes, signs and persists. All time comes from `TimeProvider`, including the token's `iat`, `nbf` and `exp`. Ids are `Guid.CreateVersion7()`. The insert is one `IUnitOfWork` commit.
- Key loading fails fast at host start (missing or unreadable PEM stops boot), but never runs when the OpenAPI generator builds the app (AD-16). The error names the path and how to fix it, e.g. `JWT signing key not found at '<path>'. Run tools/generate-dev-jwt-key (.sh or .ps1) to create one.` (decided 2026-10-14).
- Compose mounts the `./keys` folder read-only at `/run/secrets`, so `Jwt__SigningKeyPath=/run/secrets/jwt-signing-key.pem` from `.env.example` resolves inside the container (decided 2026-10-14).
- Generating the dev key needs only the .NET SDK, not `openssl`. The key logic moves to a .NET 10 file-based app, `tools/generate-dev-jwt-key.cs`, and both wrapper scripts call it. Output paths, the "already exists, skip" behaviour and the PEM formats (PKCS#8 private key, SPKI public key) stay the same (decided 2026-10-14).
- Commit the regenerated `openapi/auth-api.json` and the new EF migration. Update the docs listed in the tasks.

**Never:**
- Publishing the public key (`/.well-known/jwks.json`, `/.well-known/openid-configuration`): deferred to CAP-9 (decided 2026-10-14). `RsaSigningKey` exposes the public key and `kid` so CAP-9 can reuse it.
- Converting register to JSON: it stays `multipart/form-data` (decided 2026-10-14).
- Refresh, rotation, reuse grace or logout logic (CAP-3/4); JwtBearer validation in ServiceDefaults or `ICurrentUser` (no protected endpoint yet); rate limiting (gateway, CAP-7).
- Logging the email, password or either token. No `Database.Migrate()`. No package version outside `Directory.Packages.props`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid login | registered email (any case, padded with spaces), correct password | 200 `{accessToken, expiresAt=now+15m}`, `Set-Cookie refresh_token`, one `refresh_tokens` row | N/A |
| Wrong password | known email, bad password | 401 `invalid_credentials` | no row written, no cookie |
| Unknown email | email not registered | 401 `invalid_credentials` (identical body) | no row written, no cookie |
| No password credential | user row with no `user_credentials` row | 401 `invalid_credentials` (identical body) | no row written, no cookie |
| Invalid fields | missing or invalid email, missing password, or password > 1024 chars | 400 `validation_failed` | use case not called |
| Malformed body | invalid JSON, empty body | 400 `malformed_request` + `traceId` | use case not called |
| Wrong content type | e.g. `text/plain` to login, JSON to register | 415 `unsupported_media_type` + `traceId` | use case not called |
| Second login | same user logs in again | new, independent family; earlier rows untouched | N/A |

</frozen-after-approval>

## Code Map

- `apps/auth-api/src/Auth.Api/Endpoints/RegisterEndpoint.cs` -- pattern to copy: `Map<Feature>(this IEndpointRouteBuilder)`, `HttpResults` alias, `result.Match(..., e => e.ToProblem(httpContext))`. Login binds JSON, not `[FromForm]`.
- `apps/auth-api/src/Auth.Api/Program.cs` -- `app.MapGroup("/api/auth").MapRegister()` → chain `.MapLogin()`; it has no UseAuthentication.
- `apps/auth-api/src/Auth.Application/Features/Register/RegisterUseCase.cs` -- reuse `RegisterUseCase.NormalizeEmail`; error-definition style.
- `apps/auth-api/src/Auth.Application/Abstractions/{IUserRepository,IPasswordHasher,IUnitOfWork}.cs` -- `IPasswordHasher.Hash`/`Verify` already exist and are enough; `IUserRepository` lacks a lookup returning user + hash.
- `apps/auth-api/src/Auth.Domain/User.cs` -- private-set entity with a static factory; model `RefreshToken` the same way.
- `apps/auth-api/src/Auth.Infrastructure/{AuthDbContext,DependencyInjection,UnitOfWork}.cs`, `Entities/UserConfiguration.cs`, `Repositories/UserRepository.cs` -- DbSets, config style (snake_case, named indexes), DI (TimeProvider is already registered; the connection string is lazy).
- `apps/auth-api/src/Auth.Infrastructure/Migrations/` -- the only migration is `InitialUsers`; add the next one with `dotnet ef`.
- `libs/MyCollection.ServiceDefaults/ValidationProblemDetails.cs` -- `AddMyCollectionProblemDetails()`; `CustomizeProblemDetails` tags only `HttpValidationProblemDetails` today. Extend it here.
- `libs/MyCollection.ServiceDefaults/ErrorResults.cs` -- `Unauthorized` → 401 already mapped.
- `Directory.Packages.props` -- `CentralPackageTransitivePinningEnabled=true`; JwtBearer is pinned but no project references it.
- `tools/generate-dev-jwt-key.{sh,ps1}` -- today they call `openssl genpkey` to write `keys/jwt-signing-key.pem` (PKCS#8) and `.pub.pem` (gitignored). The `.ps1` throws when openssl isn't on PATH.
- `docker-compose.yml` auth-api -- requires `Jwt__SigningKeyPath` but **mounts no key**.
- `apps/auth-api/tests/Auth.IntegrationTests/{AuthApiFixture,RegisterEndpointTests}.cs` -- `CreateFactory` only sets the connection string, for both `Migrated` and `Unmigrated`; reuse the `Register`/`AssertProblem` helper style.
- `apps/auth-api/tests/Auth.UnitTests/Features/Register/RegisterUseCaseTests.cs` -- private nested fakes (`FixedTimeProvider` etc.); copy them, don't extract.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props` -- add `Microsoft.IdentityModel.JsonWebTokens` at the version JwtBearer 10.0.12 depends on; Infrastructure references only that, not JwtBearer -- avoids pulling the ASP.NET framework into Infrastructure and a transitive-pinning conflict.
- [x] `libs/MyCollection.ServiceDefaults/ValidationProblemDetails.cs` + `apps/auth-api/src/Auth.Api/Program.cs` -- `CustomizeProblemDetails` adds `code` by status for framework-produced bodies (400 that isn't validation → `malformed_request`, 415 → `unsupported_media_type`) and `traceId` to every one. Add `UseMyCollectionProblemDetails(this WebApplication)` (calls `UseStatusCodePages()`) and call it in Program.cs -- malformed-body handling for all endpoints.
- [x] `Auth.Domain/RefreshToken.cs` -- entity + `StartFamily(userId, tokenHash, now)` factory setting both lifetimes -- AD-8 rules in Domain.
- [x] `Auth.Application/Abstractions/{ITokenIssuer,IRefreshTokenGenerator,IRefreshTokenStore}.cs` -- `ITokenIssuer.Issue(User, DateTimeOffset now) → AccessToken(Token, ExpiresAt)` (record in the same file); generator returns `GeneratedRefreshToken(Raw, Hash)`; store `Add(RefreshToken)` -- ports.
- [x] `Auth.Application/Abstractions/IUserRepository.cs` -- add `FindForLoginAsync(normalizedEmail) → LoginCandidate(User, string? PasswordHash)?` -- login lookup.
- [x] `Auth.Application/Features/Login/{LoginCommand,LoginUseCase}.cs` -- `LoginResult(AccessToken, ExpiresAt, RefreshToken, RefreshExpiresAt)`, the `InvalidCredentials` error -- the business flow.
- [x] `Auth.Infrastructure/Security/{JwtOptions,RsaSigningKey,JwtTokenIssuer,RefreshTokenGenerator}.cs` -- see Design Notes -- adapters.
- [x] `Auth.Infrastructure/{Entities/RefreshTokenConfiguration,Repositories/RefreshTokenStore,Repositories/UserRepository,AuthDbContext,DependencyInjection}.cs` + migration `AddRefreshTokens` -- persistence + wiring.
- [x] `Auth.Api/Endpoints/{LoginEndpoint,LoginRequest,TokenResponse,RefreshTokenCookie}.cs` + `Program.cs` -- `RefreshTokenCookie.Append(HttpResponse, token, expires)` is the one place the cookie attributes live (CAP-3/4/5 reuse it); endpoint stays parse → call → map; `Produces<TokenResponse>(200)`, `ProducesProblem(400/401/415)`.
- [x] `docker-compose.yml` -- auth-api volume `./keys:/run/secrets:ro`; `appsettings.Development.json` -- `Jwt:SigningKeyPath = ../../../../keys/jwt-signing-key.pem`, resolved against the content root -- runnable locally.
- [x] `tools/generate-dev-jwt-key.cs` (new; takes the output dir as its argument; `RSA.Create(2048)`, `ExportPkcs8PrivateKeyPem`/`ExportSubjectPublicKeyInfoPem`; skips if the key exists) + `tools/generate-dev-jwt-key.{sh,ps1}` (thin `dotnet run` wrappers that pass `<repo>/keys`; `.sh` keeps `chmod 600`) -- removes the openssl dependency.
- [x] `tests/Auth.UnitTests/Features/Login/LoginUseCaseTests.cs` -- the use-case matrix rows, refresh-row lifetimes, and a second login creating a new family.
- [x] `tests/Auth.IntegrationTests/{AuthApiFixture,LoginEndpointTests,RegisterEndpointTests}.cs` -- the fixture writes a temp RSA PEM once and sets `Jwt:SigningKeyPath` for **both** factories. HTTP tests cover every matrix row: validate the JWT with the public key and assert the claims and `kid`; assert the cookie attributes from the raw `Set-Cookie` header; assert the DB row stores the hash, not the raw value; assert that two logins produce two families. Add the 415 case to register.
- [x] `apps/auth-api/openapi/auth-api.json` -- regenerated contract.
- [x] Docs:
  - `docs/auth.md`: login behaviour and failure rules.
  - `docs/local-development.md`: generate the key as an explicit step **before** the first `docker compose up`; say the scripts need only the .NET SDK.
  - `docs/conventions.md` § Endpoints and § Errors: request bodies are JSON by default and multipart only for file uploads (register); add the `malformed_request` and `unsupported_media_type` codes.
  - `docs/services.md`: login done; JWKS is CAP-9.
  - `docs/testing.md`: the fixture key setup.
  - `.env.example`: comment that `./keys` is mounted at `/run/secrets`.

**Acceptance Criteria:**
- Given a missing `Jwt:SigningKeyPath` file, when auth-api starts normally, then startup fails with the error naming the path and the generator script.
- Given no key at all, when `dotnet build` runs the OpenAPI generator, then the build succeeds.
- Given a successful login, when the access token is validated with the PEM's public key, issuer `mycollection-auth` and audience `mycollection`, then validation succeeds, `sub` equals the user id, and `iat`/`exp` match the injected clock.
- Given an empty `keys/` folder, when either wrapper script runs with only the .NET SDK installed, then both PEM files are created; when it runs again, then it reports that the key exists and changes nothing.
- Given `dotnet build`, then the solution builds with warnings-as-errors, and Application has no IdentityModel or ASP.NET reference (architecture rules hold).

## Implementation Notes

- The malformed-body handling closes the first `deferred-work.md` entry from the CAP-1 spec (framework 400/415 without `code`/`traceId`); the entry was removed.
- The OpenAPI generator does start the host, so `ValidateOnStart` failed the build. As the Design Notes allow, a `GetDocument.Insider` guard was added: `ServiceDefaults/OpenApiGeneration.IsRunning`, and `AddAuthInfrastructure(validateOnStart: !OpenApiGeneration.IsRunning)`.
- `AddMyCollectionProblemDetails` sets `RouteHandlerOptions.ThrowOnBadRequest = false`. Minimal APIs throw on a rejected body in Development, so without it 400/415 would go through the developer exception page rather than `UseStatusCodePages`.
- `Microsoft.Extensions.Options.ConfigurationExtensions` was added to CPM and Infrastructure, because `BindConfiguration` is not available outside the ASP.NET shared framework.
- Verified: build clean (warnings as errors) with `/api/auth/login` in `auth-api.json`; startup fails with the missing-key message; curl smoke for 400/415 codes; both wrappers on an empty `keys/` (create, then skip). Per CLAUDE.md, unit/integration/architecture tests are written for every matrix row but run only at PR time.

## Spec Change Log

## Review Triage Log

| # | Finding (pre-approval spec review, 2026-10-14) | Verdict | Resolution |
|---|---|---|---|
| 1 | New capability id clashes with another spec | false | User: this work stays within the platform spec; JWKS is CAP-9 there |
| 2 | Use-case-computed dummy hash breaks layering | false | User dropped the unknown-email timing check (2026-10-15); nothing to place |
| 3 | A missing mounted file becomes a host directory | false | User: mount the `./keys` folder; nothing more |
| 4 | Fail-fast mechanism vs OpenAPI generator unnamed | medium | `ValidateOnStart` + lazy key (Design Notes); fixture sets key on both factories |
| 5 | Relative IDE key path ambiguous | medium | Resolved against `ContentRootPath` |
| 6 | Generator output dir unspecified | medium | Passed as an argument by the wrappers |
| 7 | IdentityModel package choice open | medium | Pin `Microsoft.IdentityModel.JsonWebTokens` in CPM |
| 8 | Malformed JSON / 415 not covered | high | User: every endpoint must answer them properly; ServiceDefaults ProblemDetails pipeline |
| 9 | Handler stamps times from `DateTime.UtcNow` | low | Times set explicitly from `TimeProvider` |
| 10 | kid computation and RSA lifetime unpinned | low | Specified in Design Notes |
| 11 | `replaced_by_id` FK undefined | low | Nullable self-FK, `ON DELETE SET NULL` |
| 12 | Unbounded password at login | low | `[MaxLength(1024)]` |
| 13 | Omitting `name` diverges from AD-7 | low | Always present for password users; fallback deferred to CAP-5 |
| 14 | Cookie code bloats the endpoint | low | `RefreshTokenCookie.cs` helper |
| 15 | Docs and test gaps | low | Added services.md, testing.md, .env.example and second-login assertions |

| # | Finding (implementation review, 2026-10-15) | Verdict | Resolution |
|---|---|---|---|
| R1 | A garbage, public-only or unreadable PEM passes the startup check (only `File.Exists`), then every login is a 500. Frozen intent: "missing or unreadable PEM stops boot" | medium | patch: validator imports the PEM and requires a private key |
| R2 | `JwtOptionsValidator` doc says the generator never reaches `StartAsync`; it does, hence the guard | low | patch: correct the comment |
| R3 | No test for "missing key stops startup" (spec AC 1) | medium | patch: integration tests for missing and invalid key |
| R4 | No test for relative key path resolution against the content root (IDE mode) | low | patch: test the resolved options path |
| R5 | `UseStatusCodePages` gives 404/405 a ProblemDetails with `traceId` but no `code` | low | defer: before this change these had no body at all; new codes need a decision |
| R6 | Token response lacks `Cache-Control: no-store` (RFC 6749 §5.1) | medium | user approved 2026-10-16: login sets `Cache-Control: no-store` |
| R7 | OpenAPI omits `Set-Cookie`, nullable+required strings, no `format: email` | low | rejected: cosmetic; documenting the header needs a transformer |
| R8 | `LoginRequest.Email` has no `MaxLength(254)` like register | low | rejected: frozen block fixes the login shape; body size is bounded by Kestrel |
| R9 | Login reuses `RegisterUseCase.NormalizeEmail` (cross-feature reference) | low | user approved 2026-10-16: moved to `User.NormalizeEmail` in Domain; both use cases call it |
| R10 | Expired refresh rows are never deleted | low | defer: cleanup belongs with CAP-3/4 |
| R11 | Password-cap test uses an unknown email, so bcrypt never sees 1024 chars | low | patch: use a registered user |
| R12 | 400/415 tests don't assert no row/cookie | false | no user exists for those emails and the handler never runs; nothing can be written |
| R13 | No unit test for the failed-commit throw | low | rejected: bug-only path |
| R14 | `ThrowOnBadRequest = false` is a global dev-behaviour change not in docs | low | patch: document in conventions.md § Errors |
| R15 | Private key written with default umask; `.sh` chmods afterwards | false | identical to the previous openssl scripts; not a regression |
| R16 | No type-level IdentityModel rule for Domain | low | rejected: Domain is covered by `ProjectReferenceTests` |
| R17 | Body `expiresAt` keeps sub-second precision, the JWT `exp` is truncated: body up to 1 s later | low | patch: truncate `now` to whole seconds in the issuer |
| R18 | `malformed_request` also covers non-body binding failures; docs say body-only | low | patch: docs wording |
| R19 | `docker compose up` before the key exists makes a root-owned `./keys` on Linux | low | rejected: user decision (row 3), and `keys/` is gitignored; docs order key generation first |
| R20 | Missing public PEM never regenerated once the private key exists | low | rejected: unchanged from the old scripts |
| R21 | Corrupt stored bcrypt hash throws → 500 | false | hashes come only from register; unreachable state |
| R22 | Generator and wrappers have no automated test | low | defer: dev tooling, verified manually on an empty `keys/` |

## Design Notes

**Key loading.** `JwtOptions` is bound with `AddOptions<JwtOptions>().BindConfiguration("Jwt").Validate(file exists).ValidateOnStart()`, failing with the message from Boundaries. A relative path is resolved against `IHostEnvironment.ContentRootPath`. `ValidateOnStart` runs in `StartAsync`, which the OpenAPI generator never reaches. If the build proves otherwise, add an explicit `GetDocument.Insider` guard. `RsaSigningKey` is a singleton built on first resolve: `RSA.ImportFromPem`, never disposed. `kid` = `Base64UrlEncoder.Encode(JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey).ComputeJwkThumbprint())`, assigned to `RsaSecurityKey.KeyId` so it lands in the header.

**Issuing.** `JsonWebTokenHandler.CreateToken(new SecurityTokenDescriptor { Issuer, Audience, IssuedAt = now, NotBefore = now, Expires = now+15m, Claims = { sub, name, email, jti = Guid.NewGuid() }, SigningCredentials = new(key, RS256) })`.

**Malformed bodies.** Minimal APIs reject an unparseable body or an unsupported content type before `AddValidation()` runs, so no DataAnnotation applies. The framework returns a bare 400/415. `UseStatusCodePages()` with `AddProblemDetails()` turns that into ProblemDetails, and `CustomizeProblemDetails` adds this project's `code`/`traceId`. Integration tests prove the exact codes.

**Cookie** (in `RefreshTokenCookie.Append`):

```csharp
response.Cookies.Append("refresh_token", token, new CookieOptions {
    HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict,
    Path = "/api/auth", Expires = expiresAt });
```

## Verification

**Commands:**
- `dotnet build MyCollection.slnx` -- expected: success, regenerated `auth-api.json` contains `/api/auth/login`.
- `dotnet test` (at PR time only, per CLAUDE.md) -- expected: all green.
