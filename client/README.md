# Client

Angular 22 app using `@ismailza/ngx-auth-client` for the session and
`@ismailza/ngx-api-client` for API calls.

```bash
npm ci
npm start          # http://localhost:4200, /api proxied to localhost:5080
npm run test:ci    # Vitest
npm run lint       # angular-eslint, template accessibility rules included
npm run format     # Prettier
```

| Path | Contents |
| --- | --- |
| `src/main.ts` | Loads `/api/config`, then bootstraps |
| `src/app/core/config/` | Runtime config types and loader |
| `src/app/core/auth/` | `authFeatureFor()` (the adapter switch), route guards |
| `src/app/core/api/` | `provideAppApi()`, the interceptor chain, error/success handlers |
| `src/app/core/errors/` | `AppErrorHandler`: skips API errors already shown as a toast |
| `src/app/layout/` | Header, session panel, toasts |
| `src/app/features/` | One folder per page, with its own `*-api.ts` when it calls the API |

See [../docs/architecture.md](../docs/architecture.md).
