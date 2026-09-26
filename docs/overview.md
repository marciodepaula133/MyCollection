# Overview

## What MyCollection is

A platform for trading-card collectors: accounts, wishlists, and card data with aggregated prices. It has two goals of equal weight:

1. **A tool the author actually uses.**
2. **A portfolio and interview piece** that shows microservices, OAuth2/OIDC, JWT, an API gateway, a BFF, caching and Docker-based infrastructure done properly.

It is a **public Nx monorepo** of .NET 10 services behind one Nginx gateway. A web UI will join the same monorepo later.

## Slices and roadmap

| # | Slice | Contents | Status |
|---|---|---|---|
| 1 | **Identity + gateway** | auth-api: register, password login, refresh, logout, Google + Discord sign-in; Nginx gateway; one-command local env | **Current**. Spec: `spec-mycollection-platform` (CAP-1…CAP-8) |
| – | Email identity | Email verification, password reset (MailKit/SMTP, Mailpit in dev) | Planned: own spec `spec-email-identity` |
| 2 | Wishlist | wishlist-api on MongoDB; per-user wishlist CRUD | Later |
| 3 | Cards BFF | cards-bff aggregating Scryfall / TCGPlayer / CardMarket with a Redis cache | Later |
| 4 | Observability | Central logs (Seq/Loki), metrics, tracing | Later (phase 6) |
| – | Web UI | `apps/web`, types generated from committed OpenAPI | Later |

## In scope now

- auth-api (CAP-1…CAP-6), gateway (CAP-7), local environment (CAP-8)
- CI on GitHub Actions (`nx affected -t build test`)

## Explicitly out of scope now

Wishlist, Cards BFF, MongoDB, Redis, the frontend, production deployment/hosting/TLS, email sending, e2e tests, observability beyond stdout logs.

## Known accepted gap

Until email verification exists, someone could register a password account with another person's email ("email squatting"). Account **takeover** is prevented: social logins never link to unverified accounts. See [auth.md](auth.md#account-linking).
