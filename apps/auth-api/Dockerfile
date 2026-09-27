# Build context is the repo root (docker-compose.yml passes `context: .`), because Central
# Package Management needs Directory.Build.props / Directory.Packages.props alongside the
# project files, and the project references libs/*.

# ---- base SDK stage: restore + build, shared by dev/migrate/final -------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY libs/MyCollection.Results/MyCollection.Results.csproj libs/MyCollection.Results/
COPY libs/MyCollection.ServiceDefaults/MyCollection.ServiceDefaults.csproj libs/MyCollection.ServiceDefaults/
COPY apps/auth-api/src/Auth.Domain/Auth.Domain.csproj apps/auth-api/src/Auth.Domain/
COPY apps/auth-api/src/Auth.Application/Auth.Application.csproj apps/auth-api/src/Auth.Application/
COPY apps/auth-api/src/Auth.Infrastructure/Auth.Infrastructure.csproj apps/auth-api/src/Auth.Infrastructure/
COPY apps/auth-api/src/Auth.Api/Auth.Api.csproj apps/auth-api/src/Auth.Api/
RUN dotnet restore apps/auth-api/src/Auth.Api/Auth.Api.csproj

COPY libs/ libs/
COPY apps/auth-api/src/ apps/auth-api/src/
RUN dotnet publish apps/auth-api/src/Auth.Api/Auth.Api.csproj -c Release -o /app/publish --no-restore

# ---- dev: SDK + dotnet watch + source mount (docker-compose.override.yml) -----------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /src
ENV DOTNET_USE_POLLING_FILE_WATCHER=1
ENTRYPOINT ["dotnet", "watch", "run", "--no-launch-profile", "--project", "apps/auth-api/src/Auth.Api/Auth.Api.csproj"]

# ---- migrate: one-shot EF Core migration bundle (AD-12) -----------------------------------
# Built once, at image-build time, from Auth.Infrastructure's migrations (none yet - CAP-1
# adds the first one); run as the compose service `auth-migrate` against the real database,
# never via Database.Migrate() from the app.
# Built FROM the `build` stage directly (not a fresh SDK image + `COPY --from=build /src /src`):
# `dotnet restore` in `build` downloaded packages into /root/.nuget/packages, OUTSIDE /src, so
# copying only /src carries the restore *result* (project.assets.json, which references package
# paths) but not the actual package files those paths point at - `dotnet ef` then fails with
# "Package ... was not found" the moment it needs one. Continuing FROM `build` keeps the whole
# filesystem, cache included.
FROM build AS migrate
WORKDIR /src
RUN dotnet tool restore
RUN dotnet ef migrations bundle \
    --project apps/auth-api/src/Auth.Infrastructure/Auth.Infrastructure.csproj \
    --startup-project apps/auth-api/src/Auth.Infrastructure/Auth.Infrastructure.csproj \
    --output /app/efbundle \
    --force
ENTRYPOINT ["/bin/sh", "-c", "/app/efbundle --connection \"$ConnectionStrings__Postgres\""]

# ---- final: the deployable image (AD-19) ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Auth.Api.dll"]
