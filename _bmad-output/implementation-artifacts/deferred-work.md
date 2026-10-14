
- source_spec: `_bmad-output/implementation-artifacts/spec-cap-1-register-with-email-password.md`
  summary: Malformed/empty JSON bodies and wrong content types return the framework 400/415 without the stable `code`/`traceId` (AD-13).
  evidence: Binding failures happen before the endpoint's Validate(); needs AddProblemDetails customization or manual body binding.
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
