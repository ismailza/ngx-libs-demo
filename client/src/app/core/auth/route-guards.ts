import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { AuthService } from '@ismailza/ngx-auth-client';
import { RUNTIME_CONFIG } from '../config/runtime-config';

/** Keeps signed-in users away from the login and sign-up pages. */
export const guestGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.whenReady();
  return auth.authenticated() ? router.parseUrl('/') : true;
};

/**
 * The login form only exists for adapters that take credentials in-app
 * (`withHttpAuth()`). With a redirect adapter, `/login` simply doesn't match.
 */
export const canSignInInApp: CanMatchFn = () => inject(AuthService).canSignIn();

/** The sign-up form talks to the Local provider's own endpoint. */
export const usesLocalProvider: CanMatchFn = () => inject(RUNTIME_CONFIG).auth.provider === 'local';
