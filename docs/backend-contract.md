# Backend contract

What the API promises each library. The integration tests in
`server/tests/Demo.Api.Tests` check every point below.

## Runtime config

| Endpoint | Auth | Response |
| --- | --- | --- |
| `GET /api/config` | none | `{ auth: { provider, local?, keycloak? }, features: { admin, signUpForm } }`, `Cache-Control: no-store` |

`features` says which optional endpoints are mapped, so the client routes pages
by what exists instead of by provider name: `admin` → `/api/v1/admin/*`,
`signUpForm` → `POST /api/auth/register`. Both are `true` with Local and `false`
with Keycloak. See [auth-providers.md](auth-providers.md).

## Auth (Local provider) — for `withHttpAuth()`

| Endpoint | Request | Success | Failure |
| --- | --- | --- | --- |
| `POST /api/auth/login` | `{ username, password }` | `200 { accessToken, expiresIn }` + sets the refresh cookie | `401`, one message for every cause |
| `POST /api/auth/refresh` | refresh cookie, no body | `200 { accessToken, expiresIn }` + rotated cookie | `401` + cookie cleared |
| `POST /api/auth/logout` | refresh cookie | `204`, session revoked server-side, cookie cleared | — |
| `GET /api/auth/me` | bearer token | `{ id, username, email, emailVerified, firstName, lastName, attributes }` | `401` |
| `POST /api/auth/register` | `{ username, email, password, firstName?, lastName? }` | `201` | `400` with field errors |

`login` takes a username or an email: input containing `@` is looked up as an
email, anything else as a username. Usernames may only contain letters, digits,
`-`, `.` and `_` — never `@` — so a username can't claim another account's email.
`register` creates the account and its `user` role in one transaction, and stores
blank `firstName`/`lastName` as null (the token then omits `given_name`/`family_name`).

`login` and `register` are rate limited to 10 requests per minute per IP
(`RateLimiting:AuthPermitsPerMinute`). Over the limit, the API answers `429`
with `Retry-After`. Failed passwords lock an account for 5 minutes after 5 attempts.

### Access token

HS256 JWT, 5 minutes by default (`Auth:Local:Jwt:AccessTokenMinutes`):

```json
{
  "sub": "0c673612-…",
  "preferred_username": "admin",
  "email": "admin@demo.local",
  "given_name": "Ada",
  "family_name": "Admin",
  "roles": ["user", "admin"],
  "jti": "…",
  "iat": 1792346442,
  "nbf": 1792346442,
  "exp": 1792346742,
  "iss": "ngx-libs-demo",
  "aud": "ngx-libs-demo"
}
```

`roles` is **always an array**, even with one role, so every consumer reads it
the same way (the adapter's default `mapRoles` also accepts a single string).
Roles are re-read on every refresh, so a role
change reaches the client within one token lifetime.

### Refresh cookie

| Attribute | Value | Why |
| --- | --- | --- |
| `HttpOnly` | yes | Scripts — including injected ones — can't read it |
| `Secure` | yes (`RefreshToken:Secure`) | Browsers treat `http://localhost` as secure |
| `SameSite` | `Strict` | Not sent on cross-site requests |
| `Path` | `/api/auth` | Not sent with ordinary API calls |
| `Expires` | 7 days, sliding | Each refresh extends the session |

The value is 32 random bytes; only its SHA-256 is stored. Each refresh
**rotates** it. Presenting an already-rotated value:

- within 10 s of its rotation (`ReuseGraceSeconds`) **and** from the same IP and
  `User-Agent` the token was issued to, is treated as a concurrent tab and gets a
  new token in the same session;
- otherwise (later, or from another client), is treated as theft: the whole
  session (token family) is revoked, and the legitimate holder's next refresh
  fails too.

Both values can be faked, so the client check narrows the grace window rather
than closing it.

## Auth (Keycloak provider) — for `withKeycloak()`

With `Auth:Provider=Keycloak` the API issues no tokens: `/api/auth/*` and
`/api/v1/admin/*` answer `404`. Every protected endpoint accepts a Keycloak access
token that has:

| Claim | Requirement |
| --- | --- |
| `iss` | `{Auth:Keycloak:Url}/realms/{Realm}` |
| `aud` | contains `demo-api` (`Auth:Keycloak:Audience`) |
| signature | RS256, a key from the realm's JWKS |
| `exp` / `nbf` | valid, 30 s skew |
| `sub` | the user id that data is keyed by |
| `roles` | realm roles, read by the `admin` policy |

Anything else is `401 UNAUTHENTICATED`. The realm's mappers produce `aud` and
`roles` — see [auth-providers.md](auth-providers.md#realm-demo).

## Errors — for ngx-api-client

Every error under `/api` is `application/problem+json` in the shape
`apiErrorInterceptor` reads (`Infrastructure/ApiProblemDetailsWriter.cs`):

```json
{
  "type": "about:blank",
  "title": "Bad Request",
  "status": 400,
  "detail": "Some fields are invalid.",
  "instance": "/api/v1/notes",
  "code": "VALIDATION_ERROR",
  "timestamp": "2026-10-18T18:16:13.716Z",
  "traceId": "a9e4cac75f5a39cea213c87181aae5f9",
  "errors": [{ "field": "text", "message": "The Text field is required." }]
}
```

- `errors` is an **array of `{ field, message }`**, field names in camelCase.
  ASP.NET Core's default `{ "Text": ["…"] }` dictionary is flattened.
- `detail` is always present and safe to show.
- `code` defaults from the status and can be set per error:

| Status | `code` |
| --- | --- |
| 400, 422 | `VALIDATION_ERROR` |
| 401 | `UNAUTHENTICATED` |
| 403 | `ACCESS_DENIED` |
| 404 | `ENTITY_NOT_FOUND` |
| 409 | `CONFLICT` |
| 429 | `RATE_LIMITED` |
| 502, 503, 504 | `EXTERNAL_SERVICE_ERROR` |
| other 5xx | `INTERNAL_ERROR` |

## Versioning

Feature endpoints are versioned in the URL (`/api/v1/…`), matching
`provideApi({ version: 1, versioning: 'url' })`. Auth and config endpoints are
not versioned; the client calls them with `{ version: false }` where needed.

## Paging

`GET /api/v1/admin/users?page=0&size=5` returns the shape `ApiService.getPage()` expects:

```json
{ "content": [ … ], "page": { "number": 0, "size": 5, "totalElements": 12, "totalPages": 3 } }
```

`page` is zero-based; `size` is clamped to 1–100.

## Feature endpoints

| Endpoint | Auth | Notes |
| --- | --- | --- |
| `GET/POST /api/v1/notes`, `DELETE /api/v1/notes/{id}` | user | Scoped to the token's `sub` |
| `GET /api/v1/admin/users` | `admin` role | Paged. Local provider only |
| `POST /api/v1/admin/users/{id}/revoke-sessions` | `admin` role | Revokes all refresh tokens. Local only |
| `GET /api/health` | none | Includes a database check, and with Keycloak a check that the realm's discovery document can be fetched (`503` otherwise) |

### Playground (misbehaving on purpose)

All require a signed-in user.

| Endpoint | Behaviour |
| --- | --- |
| `GET /api/v1/playground/flaky?key&failures&status` | Fails `failures` times with `status` (503 by default; 429 adds `Retry-After: 1`), then `200 { attempts }`. Count is per `key`. |
| `GET /api/v1/playground/status/{code}` | That status as problem+json. `400` includes field errors; `409` has `code: STALE_VERSION`. |
| `GET /api/v1/playground/slow?ms` | Responds after `ms` (max 10 s) |
| `GET /api/v1/playground/version`, `/api/v2/…` | Same resource, different shapes per version |
