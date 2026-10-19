import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { AuthService } from '@ismailza/ngx-auth-client';
import { type Features, RUNTIME_CONFIG } from '../config/runtime-config';

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

/**
 * Routes a page only when the API serves its endpoints (`features` in
 * `/api/config`): the Admin page and the sign-up form exist with the Local
 * provider only. Without a match, the URL falls through to the `**` redirect.
 */
export const featureEnabled =
  (feature: keyof Features): CanMatchFn =>
  () =>
    inject(RUNTIME_CONFIG).features[feature];
