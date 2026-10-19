# Development

## Prerequisites

- .NET SDK 10 (`server/global.json`)
- Node 24 and npm 11
- Docker (Postgres for development, Testcontainers for the server tests)

## Run with hot reload

```bash
docker compose up -d db                                   # Postgres 18 on localhost:5433
dotnet run --project server/src/Demo.Api                  # API on http://localhost:5080
cd client && npm ci && npm start                          # app on http://localhost:4200
```

For the Keycloak provider, see [auth-providers.md](auth-providers.md#run-it).

`ng serve` proxies `/api` to `localhost:5080` (`client/proxy.conf.json`), so the
app and API share an origin, as they do behind nginx. The API applies
migrations and seeds `alice` and `admin` on startup. Development settings are in
`server/src/Demo.Api/appsettings.Development.json`.

## Everything in Docker

```bash
cp .env.example .env   # set JWT_SIGNING_KEY
docker compose up --build
```

App on <http://localhost:8080>. To start from an empty database, run
`docker compose down -v`.

## Tests, lint, format

| Where | Command | What |
| --- | --- | --- |
| `server/` | `dotnet test` | Integration tests: real API + Postgres 18 in a container |
| `server/` | `dotnet build` | Warnings are errors; .NET analyzers at `latest-recommended` |
| `client/` | `npm run test:ci` | Vitest: interceptor chain, runtime config, adapter switch |
| `client/` | `npm run lint` | angular-eslint, including template accessibility rules |
| `client/` | `npm run format` / `npm run format:check` | Prettier |

CI (`.github/workflows/ci.yml`) runs all of them, then builds both Docker images.

The server tests use one Postgres container and a `FakeTimeProvider` shared by
all tests, so they run one at a time. Tests that need time to pass (for example
the refresh grace window) call `api.Clock.Advance(...)` instead of sleeping.

## Database migrations

```bash
cd server
dotnet ef migrations add <Name> --project src/Demo.Api -o Data/Migrations
```

Migrations run on startup (`Database:MigrateOnStartup`). That suits a demo;
in production you would apply a migration bundle before deploying.

## The libraries

Both come from npm: `@ismailza/ngx-api-client` `^0.3.0` and
`@ismailza/ngx-auth-client` `^0.2.0` (the first release with the `/http` adapter).

To try an unreleased change of either library, build it and install the packed
tarball, without committing it:

```bash
(cd ../ngx-auth-client && npx ng build ngx-auth-client && cd dist/ngx-auth-client && npm pack --pack-destination /tmp)
cd client && npm install /tmp/ismailza-ngx-auth-client-*.tgz
```

## Configuration reference

Server settings come from `appsettings*.json`, overridden by environment variables
(`:` becomes `__`). The Compose file maps the `.env` values onto them.

| Setting | `.env` | Default | Notes |
| --- | --- | --- | --- |
| `Auth:Provider` | `AUTH_PROVIDER` | `Local` | `Local` or `Keycloak` (also set `COMPOSE_PROFILES=keycloak`) |
| `Auth:Local:Jwt:SigningKey` | `JWT_SIGNING_KEY` | — | Required for Local, ≥ 32 characters |
| `Auth:Local:Jwt:AccessTokenMinutes` | `ACCESS_TOKEN_MINUTES` | `5` | |
| `Auth:Local:Jwt:Issuer` / `Audience` | | `ngx-libs-demo` | |
| `Auth:Local:RefreshToken:Days` | | `7` | Sliding |
| `Auth:Local:RefreshToken:ReuseGraceSeconds` | | `10` | |
| `Auth:Local:RefreshToken:Secure` | `REFRESH_COOKIE_SECURE` | `true` (`false` in Docker) | Docker serves plain HTTP; set `true` behind HTTPS |
| Realm redirect URI | `APP_URL` | `http://localhost:8080` | Substituted into `keycloak/demo-realm.json` on first import |
| `Auth:Keycloak:Url` | `KEYCLOAK_URL` | — (`http://localhost:8180` in Development and Docker) | Public Keycloak URL; see [auth-providers.md](auth-providers.md) |
| `Auth:Keycloak:InternalUrl` / `Realm` / `ClientId` / `Audience` / `RequireHttpsMetadata` | | `Url` / `demo` / `demo-web` / `demo-api` / `true` | |
| Keycloak DB and admin console | `KEYCLOAK_DB_PASSWORD`, `KEYCLOAK_ADMIN`, `KEYCLOAK_ADMIN_PASSWORD` | `keycloak`, `kcadmin`, `kcadmin` | Compose `keycloak` profile only |
| `ConnectionStrings:Default` | `POSTGRES_PASSWORD` | — | |
| `RateLimiting:AuthPermitsPerMinute` | | `10` | Login and register, per IP |
| `Database:MigrateOnStartup` | | `true` | |
| `Seed:Enabled` / `Seed:Password` | `SEED_DEMO_USERS` / `SEED_PASSWORD` | `false` (`true` in Development) / `Passw0rd!` | Creates `alice` and `admin` (Local only). Never enable on a reachable host |
