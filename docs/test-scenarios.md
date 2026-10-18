# Test scenarios

A manual test plan for the two libraries. Keep DevTools open on **Network**
and **Application → Cookies**. Demo accounts: `alice` (user) and `admin`
(user, admin), password `Passw0rd!`.

## ngx-auth-client (HTTP adapter)

| # | Do | Expect |
| --- | --- | --- |
| 1 | Signed out, open `/notes` | `authGuard` redirects to `/login?returnUrl=%2Fnotes` |
| 2 | Sign in as alice | Back on `/notes`. A `refresh_token` cookie: HttpOnly, Secure, SameSite=Strict, Path=/api/auth |
| 3 | Reload | One `POST /api/auth/refresh`; still signed in, no login form |
| 4 | Wait on any page (5-minute tokens) | ~30 s before expiry the session panel's token id changes: a background refresh |
| 5 | Open two tabs, reload both at once | Both stay signed in (refresh grace window) |
| 6 | As alice, open `/admin` | Redirected to `/forbidden` |
| 7 | As admin (in another browser), revoke alice's sessions | Alice is signed out when her next refresh is refused |
| 8 | Sign out | `POST /api/auth/logout`, cookie removed; reload stays signed out |
| 9 | Open `/login?returnUrl=//evil.example` and sign in | You land on `/`, not on another site (`sanitizeReturnUrl`) |
| 10 | Wrong password | "Wrong username or password." (`InvalidCredentialsError`) |
| 11 | Wrong password 11 times within a minute | The 11th answers 429 (rate limit). Separately, 5 failures lock the account for 5 minutes |
| 12 | Create an account with a weak password | Identity's rules shown under the password field; a valid one signs you in |
| 13 | Stop the API (`docker compose stop server`) and reload | A startup error page (runtime config unreachable); start it again and reload |
| 14 | Signed in, stop the API, wait for the background refresh | You stay signed in: a 502 is an error, not a refusal, so the adapter keeps the session |

## ngx-api-client

Most of these are buttons on the **Playground** page.

| # | Do | Expect |
| --- | --- | --- |
| 15 | Notes page | `GET /api/v1/notes` with `Authorization: Bearer …` |
| 16 | Add / delete a note | "Note added." / "Note deleted." toasts (`ApiSuccessHandler`) |
| 17 | Add an empty note via DevTools (`POST {text:""}`) | 400 with `errors: [{ field: "text", … }]`; no toast |
| 18 | Playground → Fail twice (503) | 3 requests in Network, each with `Authorization`; one success after ≈ 1.5 s |
| 19 | Playground → Rate-limited once (429) | 2nd attempt about 1 s later: `Retry-After` wins over the backoff |
| 20 | Playground → Fail 5 times | 4 requests (1 + 3 retries), then **one** error toast |
| 21 | Playground → retry: false | 1 request, immediate failure |
| 22 | Playground → 400 / 404 / 409 | The normalised `ApiError` (`code`, `detail`, `errors`, `traceId`) shown, no toast |
| 23 | Playground → 500 | Retried, then one toast with the trace id |
| 24 | Playground → Slow request | Progress bar while it runs; none with `showLoader: false` |
| 25 | Playground → version 1 / version 2 | `/api/v1/playground/version` vs `/api/v2/…`, different response shapes |
| 26 | Admin page | `GET /api/v1/admin/users?page=0&size=5`; Next/Previous page through results |
| 27 | Revoke your own sessions as admin, wait for expiry, add a note | The refresh is refused, the API answers 401, and `ApiErrorHandler` sends you to sign in |
