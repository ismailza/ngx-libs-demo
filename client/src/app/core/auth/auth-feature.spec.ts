import { TestBed } from '@angular/core/testing';
import { KEYCLOAK_CONFIG } from '@ismailza/ngx-auth-client/keycloak';
import { describe, expect, it } from 'vitest';
import { authFeatureFor } from './auth-feature';

const keycloak = {
  provider: 'keycloak',
  keycloak: { url: 'http://kc', realm: 'demo', clientId: 'web' },
} as const;

/** The resolved `withKeycloak()` options, without starting keycloak-js. */
function keycloakConfig(baseUri: string) {
  TestBed.configureTestingModule({ providers: [...authFeatureFor(keycloak, baseUri).providers] });
  return TestBed.inject(KEYCLOAK_CONFIG);
}

describe('authFeatureFor', () => {
  it('uses the HTTP adapter for the local provider', () => {
    const feature = authFeatureFor({
      provider: 'local',
      local: {
        endpoints: {
          signIn: '/api/auth/login',
          refresh: '/api/auth/refresh',
          logout: '/api/auth/logout',
          profile: '/api/auth/me',
          register: '/api/auth/register',
        },
      },
    });

    expect(feature.kind).toBe('http');
  });

  it('uses the Keycloak adapter for the keycloak provider', () => {
    expect(authFeatureFor(keycloak, 'http://app.test/').kind).toBe('keycloak');
  });

  it('passes the realm from the runtime config and restores sessions silently', () => {
    expect(keycloakConfig('http://app.test/')).toMatchObject({
      url: 'http://kc',
      realm: 'demo',
      clientId: 'web',
      onLoad: 'check-sso',
      pkceMethod: 'S256',
      silentCheckSsoRedirectUri: 'http://app.test/silent-check-sso.html',
      postLogoutRedirectUri: 'http://app.test/',
    });
  });

  it('keeps Keycloak client roles out of auth.roles()', () => {
    expect(keycloakConfig('http://app.test/').roles).toEqual({ realm: true, resource: false });
  });

  it('resolves its pages against the base href', () => {
    const config = keycloakConfig('http://app.test/demo/');

    expect(config.silentCheckSsoRedirectUri).toBe('http://app.test/demo/silent-check-sso.html');
    expect(config.postLogoutRedirectUri).toBe('http://app.test/demo/');
  });
});
