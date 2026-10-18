import { Component, computed, inject, input, signal } from '@angular/core';
import { form, FormField, FormRoot, required } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { AuthService, InvalidCredentialsError } from '@ismailza/ngx-auth-client';
import { sanitizeReturnUrl } from '@ismailza/ngx-auth-client/http';

const DEMO_PASSWORD = 'Passw0rd!';

/** The in-app form `withHttpAuth()` navigates to. Only routed when the adapter can `signIn()`. */
@Component({
  selector: 'app-login',
  imports: [FormField, FormRoot, RouterLink],
  templateUrl: './login.html',
})
export default class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Bound from the query string by `withComponentInputBinding()`. Untrusted. */
  readonly returnUrl = input<string>();

  /** Only in-app paths get through: stops the login page being an open redirect. */
  protected readonly target = computed(() => sanitizeReturnUrl(this.returnUrl()));

  protected readonly error = signal<string | null>(null);
  private readonly model = signal({ username: '', password: '' });

  protected readonly loginForm = form(
    this.model,
    (path) => {
      required(path.username);
      required(path.password);
    },
    {
      submission: {
        action: async (field) => {
          this.error.set(null);
          // signIn() bypasses the HTTP interceptors, so no toast reports its failure: this page does.
          try {
            await this.auth.signIn(field().value());
          } catch (error) {
            this.error.set(
              error instanceof InvalidCredentialsError
                ? 'Wrong username or password.'
                : 'Sign-in is unavailable. Please try again.',
            );
            return;
          }
          await this.router.navigateByUrl(this.target());
        },
      },
    },
  );

  protected showError(field: typeof this.loginForm.username): boolean {
    return field().touched() && field().invalid();
  }

  protected fill(username: 'alice' | 'admin'): void {
    this.model.set({ username, password: DEMO_PASSWORD });
  }
}
