import { inject, Service } from '@angular/core';
import { type ApiRequestOptions, ApiService } from '@ismailza/ngx-api-client';
import { Observable } from 'rxjs';

/** The server's misbehaving `/api/v{n}/playground` endpoints. */
@Service()
export class PlaygroundApi {
  private readonly api = inject(ApiService);

  /** Fails `failures` times with `status`, then succeeds. Attempts are counted per `key`. */
  flaky(
    key: string,
    failures: number,
    status = 503,
    options?: ApiRequestOptions,
  ): Observable<unknown> {
    const query = `key=${encodeURIComponent(key)}&failures=${failures}&status=${status}`;
    return this.api.get<unknown>(`/playground/flaky?${query}`, options);
  }

  /** Answers with that status as problem+json. */
  status(code: number, options?: ApiRequestOptions): Observable<unknown> {
    return this.api.get<unknown>(`/playground/status/${code}`, options);
  }

  slow(ms: number, options?: ApiRequestOptions): Observable<unknown> {
    return this.api.get<unknown>(`/playground/slow?ms=${ms}`, options);
  }

  version(options?: ApiRequestOptions): Observable<unknown> {
    return this.api.get<unknown>('/playground/version', options);
  }
}
