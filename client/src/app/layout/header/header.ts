import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '@ismailza/ngx-auth-client';
import { RUNTIME_CONFIG } from '../../core/config/runtime-config';

/**
 * Written against capabilities, not a specific adapter: "Sign in" calls
 * `login()`, which shows the in-app form with `withHttpAuth()` and redirects
 * with `withKeycloak()`.
 */
@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './header.html',
  styleUrl: './header.css',
})
export class Header {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly runtime = inject(RUNTIME_CONFIG);

  protected readonly username = computed(() => {
    const claims = this.auth.claims();
    const name = claims?.['preferred_username'] ?? claims?.['sub'];
    return typeof name === 'string' ? name : '';
  });

  /** The adapter's own sign-up (Keycloak), or else this app's form (Local). */
  protected readonly signUp = computed<'adapter' | 'form' | null>(() => {
    if (this.auth.canRegister()) {
      return 'adapter';
    }
    return this.runtime.auth.provider === 'local' ? 'form' : null;
  });

  protected readonly isAdmin = computed(() => this.auth.roles().includes('admin'));
  protected readonly loggingOut = signal(false);
  protected readonly logoutError = signal<string | null>(null);

  protected login(): void {
    void this.auth.login();
  }

  protected register(): void {
    void this.auth.register();
  }

  protected async logout(): Promise<void> {
    this.loggingOut.set(true);
    this.logoutError.set(null);
    try {
      await this.auth.logout();
    } catch {
      // The local session is already cleared; the server may still hold the
      // refresh cookie, so a reload could sign the user back in.
      this.logoutError.set(
        'You are signed out here, but the server could not be reached. Reloading may sign you back in.',
      );
      await this.router.navigateByUrl('/');
    } finally {
      this.loggingOut.set(false);
    }
  }
}
