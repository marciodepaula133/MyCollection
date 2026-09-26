---
id: SPEC-mycollection-platform
companions:
  - stack.md
  - architecture-diagrams.md
  - ../../planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md
sources:
  - ../../../docs/history/initial-analysis-plan.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# MyCollection Platform

## Why

A vision to realize, serving two goals equally. The first is a card-collection tool the author actually wants to use: accounts, wishlists and aggregated card prices. The second is a portfolio and interview piece that shows microservices, OAuth2/OIDC, JWT, API gateway, BFF, caching and Docker-based infrastructure done properly. The product is a monorepo of .NET services behind one gateway. This first slice delivers only the foundation that every later service depends on: identity (the Auth API) and the single public entry point (the gateway).

## Capabilities

- **CAP-1**
  - **intent:** A visitor can register with email and password.
  - **success:** Registration creates a user. A duplicate email is rejected. The password is stored only as a bcrypt hash.
- **CAP-2**
  - **intent:** A user can log in with email and password and receive a short-lived access token and a long-lived refresh token.
  - **success:** Valid credentials return both tokens. Invalid credentials return 401 without saying which field was wrong.
- **CAP-3**
  - **intent:** A user can renew their session with a refresh token.
  - **success:** A valid refresh token yields a new access token. An expired or revoked refresh token is rejected.
- **CAP-4**
  - **intent:** A user can log out, which invalidates their refresh token.
  - **success:** Trying to refresh with a logged-out token fails.
- **CAP-5**
  - **intent:** A user can sign in with Google (OAuth2/OIDC).
  - **success:** The redirect-and-callback flow ends with tokens issued. The first login creates a user, and later logins resolve to the same user.
- **CAP-6**
  - **intent:** A user can sign in with Discord (OAuth2).
  - **success:** Same flow and outcome as CAP-5, using Discord.
- **CAP-7**
  - **intent:** The gateway is the only public entry point. It routes `/api/auth/*` to the Auth API and centrally applies CORS, a global rate limit and request logging.
  - **success:** The frontend completes every auth flow through the gateway alone. Requests over the limit get 429. Disallowed origins are blocked. Each request produces a log line.
- **CAP-8**
  - **intent:** A developer can run the whole slice locally with one command and iterate without rebuilding.
  - **success:** `docker compose up` starts PostgreSQL, the one-shot migration step, the Auth API and the gateway, in that order. Saving a `.cs` file hot-reloads the service. `docker compose up auth-api` runs the Auth API alone (with its database), reachable directly for Postman.

## Constraints

- Monorepo: an Nx workspace holding all services, gateway config, compose files and (later) the web UI.
- Services are .NET 10, hexagonal inside. The Auth API persists to PostgreSQL 18, keeping local credentials and social identities separate from the base user (schema in [stack.md](stack.md)).
- The gateway is Nginx.
- Passwords are never stored in plain text (bcrypt).
- Access tokens are short-lived and refresh tokens are long-lived.
- Secrets (the RS256 JWT private key, OAuth client secrets) come from environment configuration or mounted files, never from source code.
- The architecture spine (companion) is binding for how every capability is built.
- The Wishlist Service (MongoDB) and Cards BFF (Redis plus external card and price APIs) must be addable to the repo, the gateway and compose without restructuring.

## Non-goals

- Wishlist Service, Cards BFF, MongoDB, Redis and external card or price APIs. These are later slices of this same spec.
- The frontend.
- Production deployment and hosting.
- Observability beyond stdout request/application logs and health checks (central logs, metrics and tracing are roadmap phase 6).
- Email verification and password reset. They get their own spec (`spec-email-identity`).
- End-to-end tests through the gateway (unit, integration and architecture tests are in scope).

## Success signal

- From a clean clone with `.env` filled in, `docker compose up` brings the stack up healthy. `nx affected -t build test` passes, including integration tests that cover register, login, refresh, rotation reuse (grace window and family revocation), and logout-then-refresh failure against a real PostgreSQL.
- A manual run through the gateway signs in with Google and with Discord, each ending with an access token and a refresh cookie for a persisted user.

## Resolved in architecture

- JWT validation happens in each service, with RS256 keys published via JWKS; the gateway doesn't validate (spine AD-6).
- Token lifetimes: 15 min access, 30 days sliding refresh, rotated on every use with a 10 s reuse grace window (AD-8).
- Social login links automatically only between verified emails. It is refused for unverified provider emails and for unverified password accounts. Email squatting is an accepted gap until `spec-email-identity` (AD-10).
- Provider access tokens are discarded after login (AD-10).

## Open Questions

- Which card games are supported: Magic only (Scryfall), or also Pokémon and others? This doesn't block the current slice.
