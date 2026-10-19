# Server

ASP.NET Core 10 minimal API. It issues and validates tokens for the Local auth
provider, serves the demo features, and writes errors in the format
ngx-api-client reads.

```bash
docker compose up -d db            # from the repo root: Postgres on :5433
dotnet run --project src/Demo.Api  # http://localhost:5080
dotnet test                        # integration tests (needs Docker)
```

| Path | Contents |
| --- | --- |
| `src/Demo.Api/Auth/` | Provider switch, `/api/config`, Local provider, Keycloak token validation |
| `src/Demo.Api/Data/` | EF Core context, entities, migrations, seeding |
| `src/Demo.Api/Features/` | Notes, Admin, Playground endpoints |
| `src/Demo.Api/Infrastructure/` | problem+json writer, rate limiting, paging |
| `tests/Demo.Api.Tests/` | `WebApplicationFactory` + Testcontainers Postgres 18 |

See [../docs/backend-contract.md](../docs/backend-contract.md) and
[../docs/development.md](../docs/development.md).
