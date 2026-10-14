---
title: 'Switch RegisterEndpoint to DataAnnotations validation; add name/avatarUrl'
type: 'refactor'
created: '2026-10-09'
status: 'in-review'
baseline_commit: 'c0438d2babc5e2597a5aa4511c14a5c783e873da'
route: 'dispatch'
review_loop_iteration: 0
context: ['{project-root}/docs/conventions.md', '{project-root}/_bmad-output/implementation-artifacts/spec-cap-1-register-with-email-password.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `RegisterEndpoint.Validate`/`IsValidEmail` hand-roll request-shape checks (email format via `MailAddress`, password length via `string.Length`) that .NET 10's built-in Minimal API validation can do declaratively, with no extra package. Hand-rolling here sets the pattern every future endpoint would copy.

**Approach:** Annotate `RegisterRequest` with `[Required]`, `[EmailAddress]`, `[MaxLength(254)]` (email) and `[Required]`, `[StringLength(32, MinimumLength = 8)]` (password); call `builder.Services.AddValidation()` once in `Program.cs`. Delete `Validate`/`IsValidEmail`. Add a `MyCollection.ServiceDefaults` helper that makes the framework's automatic validation failure carry this project's `code`/`traceId` (AD-13), so it stays a drop-in default for every future service.

**Merged in 2026-10-09 (user chose to fold a split-off goal back in before implementation started):** `RegisterRequest` also gains `name` (`[Required, MaxLength(200)]`, flows through `RegisterCommand`/`User.Register` into the existing nullable `users.name` column, and is returned in `RegisterResponse`) and `avatar` — an actual uploaded file (`IFormFile?`), not a URL string, accepted for forward-compat but never read or persisted; `User.AvatarUrl` stays unset from this endpoint. Accepting a real file changes the endpoint's request content type from `application/json` to `multipart/form-data` (`[FromForm] RegisterRequest`, `.DisableAntiforgery()`).

## Boundaries & Constraints

**Always:**
- The automatic validation failure response must still be `400` RFC 9457 ProblemDetails carrying `code: "validation_failed"` and `traceId`, matching every other error path (AD-13, `docs/conventions.md` § Errors).
- Scope the `code`/`traceId` injection to `HttpValidationProblemDetails` only (the type the framework's automatic validation emits), via `CustomizeProblemDetails`. Do not touch or special-case `Error.ToProblem`'s existing plain-`ProblemDetails` path.
- Keep `RegisterRequest`'s properties nullable (`string?`) — annotations validate a null-bound missing field the same way `Validate` did; do not make them non-nullable to "satisfy" `[Required]`.
- The helper registering `AddProblemDetails`/`AddValidation` lives in `MyCollection.ServiceDefaults` (already the shared ASP.NET Core plumbing project per AD-13), not duplicated per service.

**Never:**
- No change to `RegisterUseCase`, `RegisterCommand`, email normalization, password hashing, or the 8–32 character business rule itself — only how the shape is checked.
- Do not attempt to restore the old email check's stricter behavior (`MailAddress.TryCreate` + exact round-trip match). `[EmailAddress]`'s regex is more permissive (e.g. it has no the old code's trim-equality requirement); that gap is accepted (decided 2026-10-09), not patched with a custom `IsValidEmail` fallback.
- Do not extend this pass to assign real `code`s to other framework-generated ProblemDetails (malformed JSON, 404, 415) — that stays in `deferred-work.md`. Turning on `AddProblemDetails()` will incidentally also format those as bodied-but-codeless ProblemDetails instead of empty responses; accepted as a free partial improvement (decided 2026-10-09), not something this pass assigns codes to.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path | Well-formed email + 8–32 char password | Unchanged: `201 Created` | N/A |
| Missing/null `email` or `password` | request body omits the field | `400` ProblemDetails, `code: validation_failed` | Automatic validation filter short-circuits before `HandleAsync` runs |
| Malformed email | e.g. `"not-an-email"` | `400` ProblemDetails, `code: validation_failed` | `[EmailAddress]` fails |
| Email over 254 chars | 255-char string | `400` ProblemDetails, `code: validation_failed` | `[MaxLength(254)]` fails |
| Password shorter than 8 / longer than 32 | e.g. `"short"` / 33 chars | `400` ProblemDetails, `code: validation_failed` | `[StringLength]` fails |
| Multiple fields invalid at once | both email and password malformed | `400` ProblemDetails, `code: validation_failed`, `errors` lists both fields | Framework collects all failures (old code returned only the first) |
| Missing `name` | request body omits `name` | `400` ProblemDetails, `code: validation_failed` | `[Required]` fails |
| `avatar` file provided | any file, e.g. a PNG | `201 Created`; `users.avatar_url` stays `NULL` | File is received and discarded, not read or stored |
| `avatar` omitted | no file part | `201 Created` (optional) | N/A |

</frozen-after-approval>

## Code Map

- `apps/auth-api/src/Auth.Api/Endpoints/RegisterEndpoint.cs` -- annotate `RegisterRequest` fields; delete `Validate`, `IsValidEmail`, `EmailMaxLength`; `HandleAsync` drops its validation call; add `Name`/`Avatar`; `[FromForm]` binding, `.DisableAntiforgery()`, `.Accepts<RegisterRequest>("multipart/form-data")`.
- `apps/auth-api/src/Auth.Api/Program.cs` -- add `builder.Services.AddValidation()` + `AddMyCollectionProblemDetails()`.
- `libs/MyCollection.ServiceDefaults/ValidationProblemDetails.cs` -- new; `AddMyCollectionProblemDetails(this IServiceCollection)`.
- `apps/auth-api/src/Auth.Domain/User.cs` -- `Register(normalizedEmail, name, now)` gains the `name` parameter.
- `apps/auth-api/src/Auth.Application/Features/Register/RegisterCommand.cs` -- `RegisterCommand`/`RegisterResult` gain `Name`.
- `apps/auth-api/src/Auth.Application/Features/Register/RegisterUseCase.cs` -- passes `command.Name` through, returns `user.Name` in the result.
- `apps/auth-api/openapi/auth-api.json` -- regenerated; request content type is now `multipart/form-data`, `avatar` is `format: binary`, optional (both `Avatar`/`AvatarUrl`-equivalent need a C# default `= null` or the generator marks them `required` despite being nullable).
- `apps/auth-api/tests/Auth.IntegrationTests/RegisterEndpointTests.cs` -- rewritten to `MultipartFormDataContent`; covers the I/O matrix above.
- `docs/conventions.md`, `docs/auth.md` -- record the annotation-validation preference, the `[FromForm]`/file-upload pattern, and the register contract change.

## Tasks & Acceptance

**Execution:**
- [x] `libs/MyCollection.ServiceDefaults/ValidationProblemDetails.cs` -- add `AddMyCollectionProblemDetails()` -- shared, so every future service's automatic validation failures carry `code`/`traceId` for free
- [x] `apps/auth-api/src/Auth.Api/Program.cs` -- call `AddValidation()` + `AddMyCollectionProblemDetails()`
- [x] `apps/auth-api/src/Auth.Api/Endpoints/RegisterEndpoint.cs` -- annotate `RegisterRequest`, delete hand-rolled validation, add `Name`/`Avatar`, switch to `[FromForm]`
- [x] `apps/auth-api/src/Auth.Domain/User.cs`, `Auth.Application/Features/Register/*.cs` -- thread `Name` through `RegisterCommand` → `User.Register` → `RegisterResult`
- [x] `apps/auth-api/openapi/auth-api.json` -- regenerate, commit the diff
- [x] `apps/auth-api/tests/Auth.IntegrationTests/RegisterEndpointTests.cs`, `Auth.UnitTests/Features/Register/RegisterUseCaseTests.cs` -- cover the I/O matrix above, including the multi-field-invalid and avatar-file cases
- [x] `docs/conventions.md` -- § Endpoints: record the annotation-validation and `[FromForm]` house rules
- [x] `docs/auth.md` -- record `name` required / `avatar` upload accepted-not-persisted

**Acceptance Criteria:**
- Given a request missing both `email` and `password`, when `POST /api/auth/register` is called, then the response is `400` with `code: validation_failed` and an `errors` object naming both fields.
- Given `dotnet build apps/auth-api/src/Auth.Api`, then `auth-api.json` has no uncommitted diff afterward.

## Implementation Notes

- The OpenAPI generator marks a nullable record property `required: true` in the schema unless it has an explicit C# default (`= null`), regardless of `[Required]`'s absence. The original `AvatarUrl` attempt needed `= null` to document correctly as optional.
- Avatar moved from a `[Url]`-validated `string?` to an actual `IFormFile?` upload mid-implementation (user decided the endpoint should receive a real file, not a URL); this changed the request content type from `application/json` to `multipart/form-data` for the whole endpoint, not just that field.
- **Real bug found by running the targeted tests, not caught by build alone:** `RegisterRequest` as a positional record (`record RegisterRequest(string? Email, ...)`) failed every request through ASP.NET Core's `[FromForm]` complex-type binder, including the happy path — `BadHttpRequestException: Missing required value for constructor parameter 'X'`, an unhandled exception bypassing `AddValidation()`/`[Required]` entirely, regardless of nullability or a `= null` default on the constructor parameter. The form binder (`CompiledComplexTypeConverter`, from the Blazor form-mapping infra it shares) requires every constructor parameter to have a value *present* in the form; it does not fall back to null like JSON binding does. Converting `RegisterRequest` to a plain class with `init`-only properties (no constructor parameters for the binder to require) fixed it: unset properties default to `null` and `[Required]` catches them normally. This only affects `[FromForm]`-bound types — JSON-bound records (still used elsewhere, e.g. none yet in this service) are unaffected.
- Ran the two directly-affected test classes only (`RegisterEndpointTests`, `RegisterUseCaseTests`), not the full suite, per the updated CLAUDE.md testing-cadence rule: all 21 pass. Full suite still pending before a PR.

## Spec Change Log

- 2026-10-09 -- Merged the deferred `name`/`avatarUrl` goal back into this spec before implementation started (user decision, nothing had been built yet) -- avoids a second near-identical spec touching the same file. KEEP: the validation-annotations approach and the `code`/`traceId` customization, unaffected by the merge.
- 2026-10-09 -- `avatarUrl` (string, `[Url]`) replaced with `avatar` (`IFormFile?`) and the endpoint's content type changed to `multipart/form-data` (user decision: the endpoint must receive an actual file, to be uploaded and stored as a URL in a future slice) -- avoids building a URL-string contract that would need a breaking change later when real upload lands.

## Review Triage Log

## Verification

**Commands:**
- `dotnet test MyCollection.slnx` -- expected: all green.
- `dotnet build apps/auth-api/src/Auth.Api` -- expected: `auth-api.json` reflects the new schema constraints, no uncommitted diff.
