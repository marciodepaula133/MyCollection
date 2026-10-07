# Database

`auth-api` owns one PostgreSQL 18 database (`auth-db`). No other service reads it (AD-1).

## Roles

| Role | Used by | Rights |
|---|---|---|
| `auth_owner` (`POSTGRES_USER`) | The `auth-migrate` bundle | Superuser in the container: creates and alters tables. |
| `auth_app` (`AUTH_APP_DB_USER`) | `auth-api` at runtime | `SELECT/INSERT/UPDATE/DELETE` on tables in `public`, including tables created later by migrations. No DDL. |

`apps/auth-api/db/init.sql` creates `auth_app`. Postgres runs it once, on an empty data volume. It reads the role name and password from the container environment (`AUTH_APP_DB_USER`, `AUTH_APP_DB_PASSWORD`) so no secret is in the file.

## Connect locally

With the stack up, `auth-db` is on the compose network only. Use:

```powershell
docker compose exec auth-db psql -U auth_owner -d auth     # as owner
docker compose exec -e PGPASSWORD=<app password> auth-db psql -h localhost -U auth_app -d auth   # as the app
```

## Useful commands

- Check the app role cannot run DDL: as `auth_app`, `CREATE TABLE x();` fails with `permission denied for schema public`.
- Reset everything, including `init.sql`: `docker compose down -v` then `docker compose up --build`.

## Persistence

Data lives in the `auth-db-data` Docker volume and survives `docker compose down`. `down -v` deletes it. Schema changes go through migrations only; see [local-development.md § Migrations](local-development.md#migrations).
