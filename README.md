# ngx-libs-demo

A full-stack application for testing two Angular libraries together against a
real backend:

| Library | Role in the app |
| --- | --- |
| [`@ismailza/ngx-auth-client`](https://github.com/ismailza/ngx-auth-client) | The session: sign-in, token refresh, `authGuard`, role checks, the bearer token. The adapter (`withHttpAuth()` today) follows the server's configuration. |
| [`@ismailza/ngx-api-client`](https://github.com/ismailza/ngx-api-client) | Every other API call: versioned URLs, retry with backoff, `problem+json` errors as `ApiError`, success toasts, loading state. |

| Folder | Stack |
| --- | --- |
| `server/` | ASP.NET Core 10 minimal API, ASP.NET Core Identity, EF Core, Postgres 18, xUnit v3 + Testcontainers |
| `client/` | Angular 22 (zoneless, signals, Signal Forms), Vitest, ESLint |
| root | `docker-compose.yml` (Postgres + API + nginx), CI, docs |

## Quick start

```bash
cp .env.example .env     # then set JWT_SIGNING_KEY: openssl rand -base64 48
docker compose up --build
```

Open <http://localhost:8080> and sign in as **alice** (role `user`) or **admin**
(roles `user`, `admin`). Both use the password `Passw0rd!`.

To work on the code with hot reload, see [docs/development.md](docs/development.md).

## What to try

[docs/test-scenarios.md](docs/test-scenarios.md) is a checklist covering both
libraries: session restore, background refresh, guards and roles, remote sign-out,
retries, error normalisation, versioning and the open-redirect check. The
**Playground** page triggers most of the API scenarios with one click.

## Documentation

| Document | Contents |
| --- | --- |
| [Architecture](docs/architecture.md) | How a request flows, the interceptor chain, project layout, design choices |
| [Auth providers](docs/auth-providers.md) | How the server picks the provider and the client follows; the Keycloak plan |
| [Backend contract](docs/backend-contract.md) | Endpoints, tokens and cookie, error format, paging, versioning |
| [Development](docs/development.md) | Running locally, tests, lint, migrations, configuration reference |
| [Test scenarios](docs/test-scenarios.md) | Manual test plan for both libraries |
| [Library feedback](docs/library-feedback.md) | Issues found in the libraries while building this demo |
