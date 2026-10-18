import type { Observable } from 'rxjs';
import type { PlaygroundApi } from './playground-api';

export interface Scenario {
  readonly id: string;
  readonly label: string;
  readonly expect: string;
  /** Receives a fresh key per run, so the server counts this run's attempts from zero. */
  readonly call: (api: PlaygroundApi, key: string) => Observable<unknown>;
}

export interface ScenarioGroup {
  readonly title: string;
  readonly intro: string;
  readonly scenarios: readonly Scenario[];
}

/** Scenarios against the server's `/api/v{n}/playground` endpoints. */
export const SCENARIO_GROUPS: readonly ScenarioGroup[] = [
  {
    title: 'Retry',
    intro:
      'GET requests failing with 408, 429 or 5xx are retried up to 3 times with exponential backoff (500 ms × 2ⁿ + jitter).',
    scenarios: [
      {
        id: 'flaky-2',
        label: 'Fail twice (503), then succeed',
        expect: '≈ 1.5 s, one success',
        call: (api, key) => api.flaky(key, 2),
      },
      {
        id: 'flaky-429',
        label: 'Rate-limited once (429 + Retry-After: 1)',
        expect: 'waits the server’s 1 s, not the backoff',
        call: (api, key) => api.flaky(key, 1, 429),
      },
      {
        id: 'flaky-5',
        label: 'Fail 5 times',
        expect: 'gives up after 3 retries, one error toast',
        call: (api, key) => api.flaky(key, 5),
      },
      {
        id: 'flaky-no-retry',
        label: 'Fail once, retry: false',
        expect: 'fails immediately',
        call: (api, key) => api.flaky(key, 1, 503, { retry: false }),
      },
    ],
  },
  {
    title: 'Errors',
    intro:
      'Every failure becomes one ApiError. These skip the global toast (skipErrorHandler) so the normalised error is shown below.',
    scenarios: [
      {
        id: 'e400',
        label: '400 with field errors',
        expect: 'errors: [{ field, message }]',
        call: (api) => api.status(400, { skipErrorHandler: true }),
      },
      {
        id: 'e404',
        label: '404',
        expect: 'code ENTITY_NOT_FOUND',
        call: (api) => api.status(404, { skipErrorHandler: true }),
      },
      {
        id: 'e409',
        label: '409 with a custom code',
        expect: 'code STALE_VERSION',
        call: (api) => api.status(409, { skipErrorHandler: true }),
      },
      {
        id: 'e500',
        label: '500 (with toast)',
        expect: 'retried 3 times, then one toast',
        call: (api) => api.status(500),
      },
    ],
  },
  {
    title: 'Loading and versioning',
    intro:
      'ApiLoadingService drives the progress bar at the top. The version goes in the URL, and can be overridden per request.',
    scenarios: [
      {
        id: 'slow',
        label: 'Slow request (2 s)',
        expect: 'progress bar while it runs',
        call: (api) => api.slow(2000),
      },
      {
        id: 'quiet',
        label: 'Slow request, showLoader: false',
        expect: 'no progress bar',
        call: (api) => api.slow(2000, { showLoader: false }),
      },
      {
        id: 'v1',
        label: 'GET /version',
        expect: '/api/v1/playground/version',
        call: (api) => api.version(),
      },
      {
        id: 'v2',
        label: 'GET /version, { version: 2 }',
        expect: '/api/v2/playground/version',
        call: (api) => api.version({ version: 2 }),
      },
    ],
  },
];
