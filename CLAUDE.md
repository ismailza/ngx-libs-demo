# ngx-libs-demo

Full-stack app that exercises two Angular libraries together against a real backend:
`@ismailza/ngx-auth-client` (session, refresh, guards, bearer token) and
`@ismailza/ngx-api-client` (every other API call: versioning, retry, `problem+json` → `ApiError`, toasts).
The point of the repo is to find integration issues in those libraries — fixes belong in the
library, workarounds here are documented in `docs/library-feedback.md`.

- `server/` — ASP.NET Core 10 minimal API, Identity, EF Core, Postgres 18, xUnit v3 + Testcontainers
- `client/` — Angular 22 (zoneless, signals, Signal Forms), Vitest, ESLint, Prettier
- root — `docker-compose.yml` (Postgres + API + nginx), CI, `docs/`

Read the matching doc before changing an area: `docs/architecture.md` (request flow, layout,
conventions), `docs/backend-contract.md` (what the API promises), `docs/auth-providers.md`,
`docs/development.md` (commands, config reference).

## Commands

| Where | Command | Notes |
| --- | --- | --- |
| root | `docker compose up -d db` | Postgres on `localhost:5433` for local dev |
| `server/` | `dotnet build NgxLibsDemo.slnx` | Warnings are errors, analyzers at `latest-recommended` |
| `server/` | `dotnet test NgxLibsDemo.slnx` | Needs Docker running (Testcontainers) |
| `server/` | `dotnet run --project src/Demo.Api` | API on :5080, migrates + seeds on startup |
| `client/` | `npm run lint` / `npm run format:check` / `npm run test:ci` / `npm run build` | Same as CI |
| `client/` | `npm start` | :4200, proxies `/api` to :5080 |

Run `/verify` to run the whole CI suite locally before declaring work done.

## Angular tooling for agents

- The official `angular-developer` skill (`.claude/skills/angular-developer/`, from
  [angular/skills](https://github.com/angular/skills), pinned in `skills-lock.json`) covers
  general Angular 22 practice. Update it with `npx skills update angular-developer`; don't hand-edit it.
- The Angular CLI MCP server (`.mcp.json`, runs the project's own `ng mcp`) provides
  `search_documentation`, `get_best_practices`, `run_target`, etc. Prefer it over memory for Angular APIs.
- **The rules in this file win over the generic skill.** In particular, app endpoints go through
  the per-feature `ApiService` wrappers, never `HttpClient`/`httpResource` directly.

## Rules that are easy to break

**Client**
- The interceptor order in `client/src/app/core/api/http.ts` is the integration:
  `apiErrorInterceptor` → `apiSuccessInterceptor` → `retryInterceptor` → `authTokenInterceptor`.
  Don't reorder it.
- Feature API calls go through a per-feature `*-api.ts` service built on `ApiService`
  (`@Service()`, `inject(ApiService)`, relative paths like `/notes` → `/api/v1/notes`).
  Never use `HttpClient` directly for app endpoints.
- No empty `catch` and no per-page error toasts: the global `ToastApiErrorHandler` reports
  `ApiError`s; pages just `await firstValueFrom(...)`. Catch only when the page shows the error itself.
- Standalone components, OnPush by default, signals + `computed()`, `rxResource` for loads,
  Signal Forms (`@angular/forms/signals`) for forms. Separate `.ts`/`.html`/`.css` per component;
  pages are `export default class` and lazy-loaded in `app.routes.ts`.
- Pages depend on `AuthService` capabilities (`canSignIn()`, `canRegister()`, …), never on a specific adapter.
- ESLint enforces: `app` selector prefix, `prefer-signals`, no explicit `public`, template
  control flow (`@if`/`@for`), self-closing tags, template a11y rules.

**Server**
- `Program.cs` is composition only. Each feature lives in `Features/<Name>/<Name>Endpoints.cs`
  as a static class with a `Map<Name>Endpoints` extension, using `TypedResults` and `Results<…>`.
- Feature code reads `sub`/`roles` from `ClaimsPrincipal` (`user.GetUserId()`); only `Auth/`
  knows how tokens are issued. Data is keyed by `sub`, not an FK to Identity users.
- Errors use the ngx-api-client `ProblemDetail` shape (`code`, `errors: [{ field, message }]`),
  written by `Infrastructure/ApiProblemDetailsWriter.cs`. Don't return ASP.NET's dictionary shape.
- Use the injected `TimeProvider`, never `DateTime.Now`/`UtcNow`. Tests advance `api.Clock`
  instead of sleeping; tests run sequentially against one shared container.
- Don't hand-edit `Data/Migrations/` — generate with `dotnet ef` (see `/ef-migration`).
- Package versions are central in `Directory.Packages.props`; `.csproj` files have no versions.

**Contract changes** touch three places together: the endpoint, an integration test in
`server/tests/Demo.Api.Tests`, and `docs/backend-contract.md`.

## Don'ts
- Don't read or print `.env` (holds secrets); `.env.example` is the template.
- Don't commit packed library tarballs or `npm install` of local `.tgz` files into `package.json`.
- Code comments explain *why*, briefly, in plain language — match the existing style.
