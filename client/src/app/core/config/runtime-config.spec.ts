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
  features: { admin: true, signUpForm: true },
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
      features: { admin: false, signUpForm: false },
    });
    expect(config.auth.provider).toBe('keycloak');
    expect(config.features).toEqual({ admin: false, signUpForm: false });
  });

  it.each([
    [null],
    [{}],
    [{ ...local, auth: { provider: 'local' } }],
    [{ ...local, auth: { provider: 'okta' } }],
    [{ auth: local.auth }],
    [{ ...local, features: { admin: 'yes', signUpForm: true } }],
  ])('rejects %j', (body) => {
    expect(() => parseRuntimeConfig(body)).toThrow();
  });

  it('loads it from the API', async () => {
    await expect(loadRuntimeConfig(respond(local))).resolves.toEqual(local);
  });

  it('fails startup when the API answers with an error', async () => {
    await expect(loadRuntimeConfig(respond({}, 502))).rejects.toThrow('502');
  });
});
