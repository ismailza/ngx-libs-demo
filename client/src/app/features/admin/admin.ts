import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { AuthService } from '@ismailza/ngx-auth-client';
import { firstValueFrom } from 'rxjs';
import { AdminApi, type UserSummary } from './admin-api';

const PAGE_SIZE = 5;

/**
 * Guarded by `data: { auth: { anyOf: ['admin'] } }` on the client and by an
 * `admin` policy on the server — the client check is UX, the server check is
 * the security boundary. The list comes from ApiService.getPage().
 */
@Component({
  selector: 'app-admin',
  templateUrl: './admin.html',
  styleUrl: './admin.css',
})
export default class Admin {
  private readonly api = inject(AdminApi);
  private readonly auth = inject(AuthService);

  protected readonly page = signal(0);

  protected readonly users = rxResource({
    params: () => ({ page: this.page() }),
    stream: ({ params }) => this.api.users(params.page, PAGE_SIZE),
  });

  protected readonly isLastPage = computed(() => {
    // value() throws while the resource is in the error state, so check hasValue() first.
    const page = this.users.hasValue() ? this.users.value().page : undefined;
    return page === undefined || page.number + 1 >= page.totalPages;
  });

  private readonly currentUserId = computed(() => this.auth.claims()?.['sub']);

  protected isMe(user: UserSummary): boolean {
    return this.currentUserId() === user.id;
  }

  protected previous(): void {
    this.page.update((page) => Math.max(page - 1, 0));
  }

  protected next(): void {
    this.page.update((page) => page + 1);
  }

  protected async revoke(user: UserSummary): Promise<void> {
    await firstValueFrom(this.api.revokeSessions(user));
    this.users.reload();
  }
}
