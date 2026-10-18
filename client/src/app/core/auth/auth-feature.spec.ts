import { describe, expect, it } from 'vitest';
import { authFeatureFor } from './auth-feature';

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

  it('reports that keycloak is not wired yet', () => {
    expect(() =>
      authFeatureFor({
        provider: 'keycloak',
        keycloak: { url: 'http://kc', realm: 'demo', clientId: 'web' },
      }),
    ).toThrow(/not wired yet/);
  });
});
