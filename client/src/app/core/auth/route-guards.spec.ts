import { TestBed } from '@angular/core/testing';
import { Route } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { routes } from '../../app.routes';
import { type Features, RUNTIME_CONFIG, type RuntimeConfig } from '../config/runtime-config';

/** Runs a route's canMatch guards the way the router would. */
function matches(path: string, features: Features): boolean {
  const route = routes.find((r) => r.path === path) as Route;
  TestBed.configureTestingModule({
    providers: [{ provide: RUNTIME_CONFIG, useValue: { features } as RuntimeConfig }],
  });
  return TestBed.runInInjectionContext(() =>
    (route.canMatch ?? []).every((guard) => (guard as () => boolean)()),
  );
}

describe('feature routes', () => {
  it.each(['admin', 'register'])('routes /%s when the API serves it', (path) => {
    expect(matches(path, { admin: true, signUpForm: true })).toBe(true);
  });

  it.each([
    ['admin', { admin: false, signUpForm: true }],
    ['register', { admin: true, signUpForm: false }],
  ])("doesn't route /%s when the API lacks it (Keycloak)", (path, features) => {
    expect(matches(path, features)).toBe(false);
  });
});
