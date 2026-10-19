import { InjectionToken } from '@angular/core';

/**
 * Served by the API at `GET /api/config`. Read before bootstrap, so the auth
 * provider is a server setting: switching it needs no client rebuild.
 */
export interface RuntimeConfig {
  readonly auth: AuthConfig;
  readonly features: Features;
}

/** Optional parts of the API, mapped or not depending on the provider. */
export interface Features {
  /** `/api/v1/admin/*` → the Admin page. */
  readonly admin: boolean;
  /** `POST /api/auth/register` → the app's own sign-up form. */
  readonly signUpForm: boolean;
}

export type AuthConfig = LocalAuthConfig | KeycloakAuthConfig;

/** The API issues the tokens itself → `withHttpAuth()`. */
export interface LocalAuthConfig {
  readonly provider: 'local';
  readonly local: {
    readonly endpoints: {
      readonly signIn: string;
      readonly refresh: string;
      readonly logout: string;
      readonly profile: string;
      readonly register: string;
    };
  };
}

/** An external Keycloak realm → `withKeycloak()`. */
export interface KeycloakAuthConfig {
  readonly provider: 'keycloak';
  readonly keycloak: {
    readonly url: string;
    readonly realm: string;
    readonly clientId: string;
  };
}

export const RUNTIME_CONFIG = new InjectionToken<RuntimeConfig>('RUNTIME_CONFIG');

export const RUNTIME_CONFIG_URL = '/api/config';

/**
 * Fetches the runtime config. Runs before Angular exists, so it uses `fetch`
 * rather than `HttpClient`.
 */
export async function loadRuntimeConfig(fetchFn: typeof fetch = fetch): Promise<RuntimeConfig> {
  const response = await fetchFn(RUNTIME_CONFIG_URL, {
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) {
    throw new Error(`GET ${RUNTIME_CONFIG_URL} failed with ${response.status}.`);
  }
  return parseRuntimeConfig(await response.json());
}

/** Validates the parts the app depends on, so a bad config fails at startup, not mid-session. */
export function parseRuntimeConfig(body: unknown): RuntimeConfig {
  const auth = isRecord(body) && isRecord(body['auth']) ? body['auth'] : null;
  const provider = auth?.['provider'];
  const features = parseFeatures(isRecord(body) ? body['features'] : undefined);

  if (provider === 'local' && isRecord(auth?.['local']) && isRecord(auth['local']['endpoints'])) {
    return { auth: auth as unknown as LocalAuthConfig, features };
  }
  if (provider === 'keycloak' && isRecord(auth?.['keycloak'])) {
    return { auth: auth as unknown as KeycloakAuthConfig, features };
  }
  throw new Error(`Unsupported auth provider in ${RUNTIME_CONFIG_URL}: ${String(provider)}.`);
}

function parseFeatures(value: unknown): Features {
  if (
    isRecord(value) &&
    typeof value['admin'] === 'boolean' &&
    typeof value['signUpForm'] === 'boolean'
  ) {
    return { admin: value['admin'], signUpForm: value['signUpForm'] };
  }
  throw new Error(`Missing or invalid "features" in ${RUNTIME_CONFIG_URL}.`);
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
