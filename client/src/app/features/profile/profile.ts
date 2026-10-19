import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AuthService } from '@ismailza/ngx-auth-client';

/**
 * `loadProfile()` asks the adapter for the profile and fills `auth.profile()`.
 * Account and password actions only show when the adapter offers them (Keycloak
 * hosts both pages; the HTTP adapter has neither).
 */
@Component({
  selector: 'app-profile',
  imports: [DatePipe],
  templateUrl: './profile.html',
  styleUrl: './profile.css',
})
export default class Profile implements OnInit {
  protected readonly auth = inject(AuthService);
  protected readonly error = signal<string | null>(null);
  /** These redirect to the provider; a failure is not an ApiError, so no global toast reports it. */
  protected readonly actionError = signal<string | null>(null);

  protected readonly memberSince = computed(() => {
    const value = this.auth.profile()?.attributes?.['memberSince'];
    return typeof value === 'string' ? value : null;
  });

  ngOnInit(): void {
    void this.load();
  }

  protected manageAccount(): Promise<void> {
    return this.redirect(() => this.auth.accountManagement(), 'the account page');
  }

  protected updatePassword(): Promise<void> {
    return this.redirect(() => this.auth.updatePassword(), 'the password page');
  }

  private async redirect(action: () => Promise<void>, page: string): Promise<void> {
    this.actionError.set(null);
    try {
      await action();
    } catch {
      this.actionError.set(
        `Could not open ${page}. Check that the identity provider is reachable.`,
      );
    }
  }

  protected async load(): Promise<void> {
    this.error.set(null);
    try {
      await this.auth.loadProfile();
    } catch {
      this.error.set('Could not load the profile.');
    }
  }
}
