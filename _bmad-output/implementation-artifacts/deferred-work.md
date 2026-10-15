
- source_spec: `_bmad-output/implementation-artifacts/spec-cap-1-register-with-email-password.md`
  summary: No test runs init.sql or connects as the DML-only auth_app role; readiness and register under least privilege are only verified manually.
  evidence: AuthApiFixture uses the Testcontainers superuser; a compose smoke test in CI would close it. Manual check of init.sql in a throwaway container passed on 2026-10-07.
- source_spec: `_bmad-output/implementation-artifacts/spec-cap-1-register-with-email-password.md`
  summary: Database outage during register surfaces as an unstructured 500 rather than Unavailable (503); password length counts UTF-16 units; email uniqueness relies on app-side lower-casing (no citext/CHECK); duplicate-email 409 allows account enumeration; no rate limiting yet (CAP-7).
  evidence: Raised by review layers; real but outside this slice's matrix, each needs a product or architecture decision.
- source_spec: `_bmad-output/implementation-artifacts/spec-cap-1-register-with-email-password.md`
  summary: ErrorResults status mapping is only tested for Validation and Conflict.
  evidence: No endpoint yet returns the other kinds; add a theory test over Enum.GetValues<ErrorKind>() with the next consumer.

- source_spec: `_bmad-output/implementation-artifacts/spec-register-first-last-name.md`
  summary: Cap request size for the anonymous multipart register endpoint (avatar is discarded but still buffered).
  evidence: No RequestSizeLimit/form limits on POST /register; found in review.
- source_spec: `_bmad-output/implementation-artifacts/spec-register-first-last-name.md`
  summary: Trim first/last name before storing, and document validation error keys (PascalCase) plus the 400 schema in OpenAPI.
  evidence: " Ana " is stored padded; ProducesProblem(400) does not describe `errors`/`code`; found in review.
- source_spec: `_bmad-output/implementation-artifacts/spec-cap-2-login-with-email-password.md`
  summary: Bodyless framework errors other than 400/415 (404 unknown route, 405 wrong method) now get ProblemDetails with `traceId` but no `code` (AD-13).
  evidence: UseStatusCodePages covers every status; CustomizeProblemDetails only maps 400/415. Before CAP-2 these had no body at all. Needs a decision on codes such as `not_found`/`method_not_allowed`.
- source_spec: `_bmad-output/implementation-artifacts/spec-cap-2-login-with-email-password.md`
  summary: Token responses (login, later refresh) should send `Cache-Control: no-store` (RFC 6749 §5.1), as ASP.NET Core Identity's bearer endpoints do.
  evidence: Not in the CAP-2 spec, so not added silently (CLAUDE.md); raised to the user at review.
- source_spec: `_bmad-output/implementation-artifacts/spec-cap-2-login-with-email-password.md`
  summary: Expired and revoked `refresh_tokens` rows are never deleted; every login adds one.
  evidence: No cleanup job or delete path exists; decide with CAP-3/4 (refresh/logout).
- source_spec: `_bmad-output/implementation-artifacts/spec-cap-2-login-with-email-password.md`
  summary: `tools/generate-dev-jwt-key.cs` and its wrappers have no automated test.
  evidence: Verified manually on an empty keys/ folder on 2026-10-15 (create, then skip); a CI smoke step could run it into a temp dir.
