import { describe, expect, it } from 'vitest';
import { loadRuntimeConfig, parseRuntimeConfig } from './runtime-config';

const local = {
  auth: {
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
  },
};

const respond =
  (body: unknown, status = 200): typeof fetch =>
  () =>
    Promise.resolve(new Response(JSON.stringify(body), { status }));

describe('runtime config', () => {
  it('accepts the local provider', () => {
    expect(parseRuntimeConfig(local).auth.provider).toBe('local');
  });

  it('accepts the keycloak provider', () => {
    const config = parseRuntimeConfig({
      auth: {
        provider: 'keycloak',
        keycloak: { url: 'http://kc', realm: 'demo', clientId: 'web' },
      },
    });
    expect(config.auth.provider).toBe('keycloak');
  });

  it.each([[null], [{}], [{ auth: { provider: 'local' } }], [{ auth: { provider: 'okta' } }]])(
    'rejects %j',
    (body) => {
      expect(() => parseRuntimeConfig(body)).toThrow();
    },
  );

  it('loads it from the API', async () => {
    await expect(loadRuntimeConfig(respond(local))).resolves.toEqual(local);
  });

  it('fails startup when the API answers with an error', async () => {
    await expect(loadRuntimeConfig(respond({}, 502))).rejects.toThrow('502');
  });
});
