# MyCollection docs: index

Start here. Each entry says **what questions the file answers**, so you (or an AI agent) can open only the file you need.

> **Source of truth order:** architecture spine (binding decisions, `AD-n`) → spec (what to build) → these docs (explanations and how-tos). If a doc disagrees with the spine, the spine wins. Fix the doc.

## Project docs (`docs/`)

| File | Read it when you need to know… |
|---|---|
| [overview.md](overview.md) | What MyCollection is, why it exists, the slices and roadmap, what's in or out of scope right now. |
| [architecture.md](architecture.md) | The big picture: services, gateway, hexagonal layers, and a one-line index of every architecture decision (`AD-1`…`AD-22`) with links. |
| [services.md](services.md) | Which services exist or are planned, their routes, ports, databases and status. |
| [auth.md](auth.md) | How login works end to end: JWT/RS256/JWKS, refresh-token rotation and grace window, cookies, Google/Discord linking rules, what's stored. |
| [conventions.md](conventions.md) | How to write code here: project layout per service, endpoints, `Result<T>`, error format, naming, JSON, config, logging. |
| [testing.md](testing.md) | Test layers (unit, integration, architecture), tools, what goes where. |
| [local-development.md](local-development.md) | How to run things: full stack, one service, IDE debugging, ports, secrets, migrations, logs, health checks. |
| [glossary.md](glossary.md) | Terms used across the docs (BFF, JWKS, port/adapter, ProblemDetails, UUIDv7…). |
| [history/initial-analysis-plan.md](history/initial-analysis-plan.md) | The original planning notes (PT-BR). **Historical.** Superseded wherever the spine differs (e.g. `Jwt__Secret` → RS256 key, Postgres 16 → 18, `docker-compose.dev.yml` → `docker compose up`). |

## Planning artifacts (`_bmad-output/`)

| Artifact | Path | Read it when… |
|---|---|---|
| **Architecture spine** (binding) | [`_bmad-output/planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md`](../_bmad-output/planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/ARCHITECTURE-SPINE.md) | You need the exact rule for a decision (`AD-n`), pinned versions, or what's deferred. |
| Architecture decision log | [`…/architecture-MyCollection-API-2026-09-26/.memlog.md`](../_bmad-output/planning-artifacts/architecture/architecture-MyCollection-API-2026-09-26/.memlog.md) | You need **why** a decision was made. |
| **Platform spec** | [`_bmad-output/specs/spec-mycollection-platform/SPEC.md`](../_bmad-output/specs/spec-mycollection-platform/SPEC.md) | You need the capabilities (`CAP-n`) and success criteria of the current slice. |
| Spec companions | [`stack.md`](../_bmad-output/specs/spec-mycollection-platform/stack.md), [`architecture-diagrams.md`](../_bmad-output/specs/spec-mycollection-platform/architecture-diagrams.md) | Endpoint lists, schema sketch, later-slice interfaces. |
| **Email-identity spec** (phase 2, not yet built) | [`_bmad-output/specs/spec-email-identity/SPEC.md`](../_bmad-output/specs/spec-email-identity/SPEC.md) | You need the capabilities for email verification, unconfirmed-account login blocking and expiry, password reset, or the social-login reclaim of a squatted account (`CAP-9`…`CAP-16`). |
| Scaffold build record | [`_bmad-output/implementation-artifacts/spec-mycollection-platform-setup.md`](../_bmad-output/implementation-artifacts/spec-mycollection-platform-setup.md) | You need to know exactly what the initial repo/service scaffold set up, a real bug found and fixed during that build (the gateway's login/register CORS handling), or what's still pending verification. |

## Quick lookup

| Question | Go to |
|---|---|
| "Where does this code belong (Domain, Application, Infrastructure, Api)?" | [conventions.md § Layers](conventions.md#layers) |
| "What error response shape do I return?" | [conventions.md § Errors](conventions.md#errors) |
| "How do services validate a JWT?" | [auth.md § Access tokens](auth.md#access-tokens) |
| "Can service X read service Y's database?" | No. See [architecture.md](architecture.md) (AD-1). |
| "How do I add a NuGet package?" | [conventions.md § Packages](conventions.md#packages) |
| "Which port is which?" | [services.md](services.md) |
| "How do I run/debug only auth-api?" | [local-development.md](local-development.md) |
| "What's deliberately *not* decided yet?" | Spine § Deferred |

## Keeping this index honest

When you add or rename a doc, add or update its row here in the same change. Keep docs short and link to the spine rather than restating rules.
