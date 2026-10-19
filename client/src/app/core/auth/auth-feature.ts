import type { AuthFeature } from '@ismailza/ngx-auth-client';
import { withHttpAuth } from '@ismailza/ngx-auth-client/http';
import { withKeycloak } from '@ismailza/ngx-auth-client/keycloak';
import type { AuthConfig } from '../config/runtime-config';

/** Static page Keycloak loads in a hidden iframe to restore a session without a redirect. */
export const SILENT_CHECK_SSO_PAGE = 'silent-check-sso.html';

/**
 * The only place that knows which adapter is in use. Everything else talks to
 * `AuthService` and checks capabilities (`canSignIn()`, `canRegister()`, …).
 */
export function authFeatureFor(config: AuthConfig, baseUri = document.baseURI): AuthFeature {
  switch (config.provider) {
    case 'local':
      return withHttpAuth({
        endpoints: {
          signIn: config.local.endpoints.signIn,
          refresh: config.local.endpoints.refresh,
          logout: config.local.endpoints.logout,
          profile: config.local.endpoints.profile,
        },
      });

    case 'keycloak':
      return withKeycloak({
        url: config.keycloak.url,
        realm: config.keycloak.realm,
        clientId: config.keycloak.clientId,
        // Restores an existing Keycloak session on reload without leaving the page; with no
        // session the user simply stays signed out.
        onLoad: 'check-sso',
        // Relative to <base href>, so the app also works under a sub-path.
        silentCheckSsoRedirectUri: new URL(SILENT_CHECK_SSO_PAGE, baseUri).href,
        // Return to the home page after sign-out, not to a page the guard would bounce.
        postLogoutRedirectUri: new URL('./', baseUri).href,
        // Realm roles only: client roles are Keycloak's own (`account`: manage-account, …).
        roles: { realm: true, resource: false },
      });
  }
}
