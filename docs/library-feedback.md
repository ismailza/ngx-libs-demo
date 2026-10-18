# Library feedback

Issues found while integrating the libraries into this demo. Each one has a
workaround in the demo; the fix belongs in the library.

## ngx-api-client 0.3.0

### 1. The `provideApi()` JSDoc example orders interceptors so that retries never run

The README says to keep `apiErrorInterceptor` before `retryInterceptor` and the
auth interceptor last. The JSDoc example on `provideApi()` (`src/lib/provide-api.ts`)
does the opposite:

```ts
withInterceptors([
  bearerTokenInterceptor, // your auth library's bearer-token interceptor goes first
  retryInterceptor,
  apiErrorInterceptor,
  apiSuccessInterceptor,
]);
```

With that order `apiErrorInterceptor` turns the `HttpErrorResponse` into an
`ApiError` before `retryInterceptor` sees it, so nothing is retried, and the
token is not re-read per attempt. The demo's test
`signs every retried attempt and reports the failure once` fails with this order.

**Fix:** align the JSDoc with the README.

### 2. `ApiError.errors` is copied from the body without checking its shape

`normalizeError()` does `errors: body.errors`. ASP.NET Core (and other
frameworks) send validation errors as `{ "Field": ["message"] }`, so at runtime
`ApiError.errors` holds an object while its type says `ApiFieldError[]`. Code
like `error.errors?.map(...)` then throws.

**Workaround here:** the server flattens errors (`ApiProblemDetailsWriter`).

**Fix:** either normalise the common dictionary shape into
`[{ field, message }]`, or drop `errors` when it isn't an array, and/or accept a
`mapError` option in `provideApi()` — similar to ngx-auth-client's
`mapTokenResponse`.

## ngx-auth-client 0.2.0

No issues found. The defaults matched an ASP.NET Core backend once the server
followed the documented contract. One note for the docs:

- Combined with ngx-api-client, the bearer allowlist (`/^\/api(\/.*)?$/`) only
  matches when `provideApi({ baseUrl: '' })` produces relative URLs. An absolute
  `baseUrl` silently sends requests without a token. A cross-reference between
  the two READMEs would help.
