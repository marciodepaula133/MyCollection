-- Runs once, as auth_owner, the first time the auth-db data volume is created (Postgres
-- entrypoint behaviour). Creates the DML-only role auth-api uses at runtime (AD-11, AD-12).
-- To re-run after editing: `docker compose down -v`, or run it by hand with psql.
--
-- Role name and password come from the container environment so no secret lives in this file.
\getenv app_user AUTH_APP_DB_USER
\getenv app_password AUTH_APP_DB_PASSWORD
\getenv db_name POSTGRES_DB

CREATE ROLE :"app_user" LOGIN PASSWORD :'app_password';

GRANT CONNECT ON DATABASE :"db_name" TO :"app_user";
GRANT USAGE ON SCHEMA public TO :"app_user";

-- Tables are created later by the migration bundle (as auth_owner), so grant on future
-- tables. No CREATE on the schema, so the app role cannot run DDL.
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app_user";
