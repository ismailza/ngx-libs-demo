import { inject, Service } from '@angular/core';
import { ApiService } from '@ismailza/ngx-api-client';
import { Observable } from 'rxjs';

export interface RegisterRequest {
  readonly username: string;
  readonly email: string;
  readonly password: string;
  readonly firstName: string;
  readonly lastName: string;
}

/** Sign-up for the Local provider: `withHttpAuth()` has no `register()`. */
@Service()
export class AccountApi {
  private readonly api = inject(ApiService);

  /** Unversioned: `/api/auth/register`. */
  register(request: RegisterRequest): Observable<unknown> {
    return this.api.post('/auth/register', request, {
      version: false,
      successMessage: 'Account created.',
      // A POST that created an account must not be replayed.
      retry: false,
    });
  }
}
