# Glossary

| Term | Meaning here |
|---|---|
| **Adapter** | An implementation of a port that talks to the outside world (EF Core repository, bcrypt hasher, Google handler). *Driving* adapters call into the app (HTTP endpoints); *driven* adapters are called by it (DB, crypto). Lives in `Infrastructure` or `Api`. |
| **BFF (Backend for Frontend)** | A service that aggregates several upstream APIs into exactly the shape a screen needs. Here: cards-bff (later). |
| **Composition root** | The single place where interfaces are wired to implementations: `Program.cs` in `<Svc>.Api`. |
| **Family (refresh tokens)** | The chain of refresh tokens produced by rotation from one login. Theft detection revokes the whole family. |
| **Forwarded headers** | `X-Forwarded-For/Proto/Host`, set by the gateway so services know the original client IP, scheme and host. Trusted only from the gateway. |
| **Grace window** | 10 s during which a just-consumed refresh token yields a new sibling token instead of triggering theft detection. |
| **Hexagonal architecture** | Ports & adapters: business logic in the center, independent of frameworks; I/O plugs in through interfaces. |
| **OpenID configuration** | Discovery document at `/.well-known/openid-configuration` telling token validators the issuer and where the JWKS lives. |
| **JWKS** | JSON Web Key Set: the standard JSON format for publishing public keys, served by auth-api at `/.well-known/jwks.json`. |
| **JWT** | Signed JSON token carrying claims (`sub`, `exp`…). Encoded, **not** encrypted: anyone can read it; only the key holder can sign it. |
| **`kid`** | Key id in a JWT header, naming which key signed it (enables key rotation). |
| **Migration bundle** | A standalone executable built by `dotnet ef migrations bundle` that applies migrations. Run as the `<svc>-migrate` compose service. |
| **OpenAPI** | Machine-readable description of an HTTP API. Generated at build, committed, and used for Scalar and client generation. |
| **Port** | An interface declared by `Application` for something it needs (`IUserRepository`). |
| **ProblemDetails** | RFC 9457 standard JSON error shape. We add a stable `code`. |
| **RS256** | JWT signing with an RSA key pair: private key signs, public key verifies. (HS256 would use one shared secret for both.) |
| **`Result<T>`** | Return type of use cases: either a value or a typed error. Expected failures are values, not exceptions. |
| **Silent refresh** | Calling `/api/auth/refresh-token` on page load; the browser sends the HttpOnly cookie automatically. |
| **Testcontainers** | Library that starts throwaway Docker containers (e.g. Postgres) for integration tests. |
| **UUIDv7** | UUID whose first bits are a timestamp: globally unique, but sorted by creation time (index-friendly). |
