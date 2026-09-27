---
id: SPEC-email-identity
companions:
  - ../../planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md
sources: []
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Email Identity

## Why

A mandate to meet: `spec-mycollection-platform` explicitly deferred email verification and password reset, and accepted a known gap under AD-10 — a password registration can squat on someone else's email address, blocking that person's later registration or social sign-in with their own email. This is phase 2 of the platform spec, scoped to auth-api alone: it gives password accounts a real identity proof (verification), a recovery path (reset), and closes the squatting gap by letting a verified social login reclaim a squatted, still-unverified account.

## Capabilities

- **CAP-9**
  - **intent:** A newly password-registered user receives an email to confirm their address.
  - **success:** Registration triggers exactly one verification email to the registered address, carrying a token that identifies the user and expires.
- **CAP-10**
  - **intent:** A user confirms their email by following the verification link.
  - **success:** A valid, unexpired token sets `email_verified_at` and cannot be reused. An expired or invalid token is rejected with a stable error code, not a 500.
- **CAP-11**
  - **intent:** An unconfirmed user can request the verification email again.
  - **success:** A new email is sent and any prior outstanding verification token for that user is invalidated. An already-verified account gets a no-op response, never a second email.
- **CAP-12**
  - **intent:** A user who forgot their password requests a reset email by submitting their address.
  - **success:** If the address belongs to a password account, a reset email is sent. The response is identical whether or not the address is registered, so the endpoint never reveals which emails exist.
- **CAP-13**
  - **intent:** A user sets a new password using the token from the reset email.
  - **success:** A valid, unexpired, unused token lets the user set a new bcrypt-hashed password and revokes all of the user's refresh-token families, forcing re-login everywhere. An expired, invalid, or reused token is rejected.
- **CAP-14**
  - **intent:** A verified social login reclaims a squatted account: when a provider-verified email matches an existing *unverified* password account, the legitimate owner takes over instead of AD-10's current "refused."
  - **success:** Reclaim happens automatically, inline in OAuth `/complete` (same flow as a normal social sign-in, no extra confirmation step). The social identity is linked to the account and the account becomes social-only: the squatter's `user_credentials` row is deleted outright, so the squatter's password no longer grants access. Unlike normal verified↔verified linking, both credentials never coexist here, since the password was never proven to belong to the email's real owner.
- **CAP-15**
  - **intent:** An unconfirmed password account cannot log in.
  - **success:** A login attempt against an account with `email_verified_at` still null is rejected with a stable, distinct error code (not the generic invalid-credentials error), regardless of whether the password is correct.
- **CAP-16**
  - **intent:** A password registration that is never confirmed eventually frees its email address, so a squatter can't block the real owner forever.
  - **success:** An account still unconfirmed 30 days after registration no longer blocks that email: a fresh password registration or an unimpeded social login/sign-up with that address succeeds. CAP-11 (resend verification) stays available at any point before the 30 days elapse.

## Constraints

- Verification and reset tokens are opaque, single-use, and time-limited, following AD-8's posture: only a hash of the token is stored, never the raw value. Exact TTL and token format are implementation detail for architecture, not fixed here.
- Delivery goes through the same SMTP path as the rest of the platform: MailKit, Mailpit in dev, Brevo as the production relay. Templates are HTML built in C# application code and sent as the SMTP body — never a provider dashboard editor — so the relay stays swappable by SMTP host/credentials alone.
- Emails are trimmed and lower-cased before store or compare, and `users.email` stays unique (AD-10, AD-11) — this spec does not change that shape, only what sets and checks `email_verified_at`.
- Applies the existing hexagonal split and conventions (AD-2, AD-13): `Result<T>` in Application, ProblemDetails with a stable snake_case `code` at the edge, no EF/SMTP types outside Infrastructure.
- CAP-12 must not leak account existence through timing or response shape differences between a registered and unregistered email.
- CAP-14's reclaim deletes the squatter's `user_credentials` row rather than marking it inactive — password and linked social identity never both validly point to the same account when the password was set by an unverified squatter (contrast with normal verified↔verified linking, where both stay valid because the same real owner controls each).
- CAP-16's 30-day expiry frees the *email*, not the row immediately — how the expired row itself is cleaned up (soft-delete vs. hard-delete, background sweep vs. checked at registration time) is implementation detail for architecture.

## Non-goals

- Changing the email address of an already-verified account.
- Admin or manual account-merge tooling.
- Any non-email delivery channel (SMS, push).
- Production TLS/hosting concerns (already a spine-level deferred item, unrelated to this spec).
- Rate-limiting specifics beyond the gateway's existing global and `/api/auth/*` limits (AD-14 already covers this path).

## Success signal

- Integration tests (real PostgreSQL + faked SMTP/Mailpit) cover: register → verify → login unaffected; login blocked with a distinct error while unconfirmed (CAP-15); an expired or reused verification/reset token is rejected; the forgot-password response is identical for an existing vs. a non-existent email; a successful reset revokes all of the user's sessions; CAP-14 reclaim deletes the squatter's credential and links the social identity in one automatic flow; and an unconfirmed registration older than 30 days no longer blocks a fresh registration or social sign-up with that email.
- Manual check: running `docker compose up`, Mailpit shows the verification and reset emails end-to-end for a real registration and reset.

## Open Questions

- What happens to any data the squatter created under a reclaimed account before reclaim (e.g. a later-slice wishlist)? Not addressed by CAP-14, since no such data exists yet in the current slice — revisit once a service that attaches user data exists.
