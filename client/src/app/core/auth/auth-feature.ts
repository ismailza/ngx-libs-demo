import type { AuthFeature } from '@ismailza/ngx-auth-client';
import { withHttpAuth } from '@ismailza/ngx-auth-client/http';
import type { AuthConfig } from '../config/runtime-config';

/**
 * The only place that knows which adapter is in use. Everything else talks to
 * `AuthService` and checks capabilities (`canSignIn()`, `canRegister()`, …).
 */
export function authFeatureFor(config: AuthConfig): AuthFeature {
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
      // TODO(keycloak): `npm i keycloak-js`, then
      //   return withKeycloak({ url, realm, clientId, onLoad: 'check-sso' });
      // from '@ismailza/ngx-auth-client/keycloak'. See docs/auth-providers.md.
      throw new Error('The Keycloak provider is not wired yet. See docs/auth-providers.md.');
  }
}
