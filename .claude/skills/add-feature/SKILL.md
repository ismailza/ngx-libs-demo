---
name: add-feature
description: Add a full-stack feature or endpoint to the demo — server minimal-API endpoint, integration test, backend-contract doc, client *-api.ts service, page and route. Use when asked to add an endpoint, page, resource or scenario that spans server and client.
---

# Add a full-stack feature

Follow the existing Notes feature as the reference implementation:
`server/src/Demo.Api/Features/Notes/NotesEndpoints.cs`, `client/src/app/features/notes/`.

## 1. Server endpoint

- New folder `server/src/Demo.Api/Features/<Name>/` with `<Name>Endpoints.cs`:
  a `public static class` exposing `Map<Name>Endpoints(this IEndpointRouteBuilder app)`.
- Group under `/api/v1/<resource>` with `.WithTags(...)` and `.RequireAuthorization()` (or a role
  policy — see `Features/Admin`).
- Request/response types are `sealed record`s next to the endpoints; validate with
  DataAnnotations (`AddValidation()` is on). Return `TypedResults` with `Results<…>` signatures.
- Owner data is keyed by `user.GetUserId()` (the `sub` claim). Time comes from the injected
  `TimeProvider`. Pass the `CancellationToken` through to EF calls.
- Call `app.Map<Name>Endpoints()` from `Program.cs` (composition only).
- New entity? Add it under `Data/Entities/`, a `DbSet` in `AppDbContext`, then run the
  `ef-migration` skill.

## 2. Integration test

Add tests in `server/tests/Demo.Api.Tests` using `ApiFactory`/`HttpExtensions`
(`_client.SignInAsync("alice")`, `_client.SendAsync(method, url, token, body)`, `response.JsonAsync()`).
Cover: success, 401 without a token, validation error shape (`code: VALIDATION_ERROR`,
`errors: [{ field, message }]`), and per-user scoping when relevant. Advance `api.Clock` instead of sleeping.

## 3. Contract doc

Add the endpoint(s) to `docs/backend-contract.md` in the existing table format. If the feature
is a manual test case for either library, add it to `docs/test-scenarios.md`.

## 4. Client

- `client/src/app/features/<name>/<name>-api.ts`: `@Service()` class, `inject(ApiService)`,
  relative paths (`/notes`, not `/api/v1/notes`), `successMessage` on mutating calls.
- Page `<name>.ts` / `.html` / `.css`: `export default class`, selector `app-<name>`,
  `rxResource` for loads, Signal Forms for input, `await firstValueFrom(...)` for mutations
  with no error-toast code (the global handler reports errors). Template uses `@if`/`@for`
  and must pass the a11y lint rules.
- Lazy route in `client/src/app/app.routes.ts` with `title` and `canActivate: [authGuard]`
  (plus role data for admin-only pages, as the `admin` route does). Add a header link in
  `client/src/app/layout/header/` if it's a top-level page.

## 5. Verify

Run the `verify` skill for both sides.
