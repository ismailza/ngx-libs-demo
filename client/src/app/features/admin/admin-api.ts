import { inject, Service } from '@angular/core';
import { ApiService, type PaginatedResponse } from '@ismailza/ngx-api-client';
import { Observable } from 'rxjs';

export interface UserSummary {
  readonly id: string;
  readonly username: string;
  readonly email: string;
  readonly roles: readonly string[];
  readonly activeSessions: number;
  readonly createdAt: string;
}

/** User and session management of the Local provider. */
@Service()
export class AdminApi {
  private readonly api = inject(ApiService);

  users(page: number, size: number): Observable<PaginatedResponse<UserSummary>> {
    return this.api.getPage<UserSummary>('/admin/users', page, size);
  }

  revokeSessions(user: UserSummary): Observable<number> {
    return this.api.post<number>(`/admin/users/${user.id}/revoke-sessions`, null, {
      successMessage: `Signed ${user.username} out everywhere.`,
    });
  }
}
