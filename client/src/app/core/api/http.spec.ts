import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { type ApiError } from '@ismailza/ngx-api-client';
import { AUTH_PROVIDER, provideAuth } from '@ismailza/ngx-auth-client';
import { FakeAuthProvider, withFakeAuth } from '@ismailza/ngx-auth-client/testing';
import { firstValueFrom } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AccountApi } from '../../features/auth/register/account-api';
import { NotesApi } from '../../features/notes/notes-api';
import { Notifications } from '../notifications/notifications';
import { httpInterceptors, provideAppApi } from './http';

/** Lets the auth interceptor's `whenReady()` / `getToken()` promises settle. */
const settle = () => vi.advanceTimersByTimeAsync(0);

const unavailable = { status: 503, statusText: 'Service Unavailable' };

describe('ngx-api-client × ngx-auth-client', () => {
  let http: HttpTestingController;
  let auth: FakeAuthProvider;
  let notes: NotesApi;
  let notifications: Notifications;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        provideAuth(withFakeAuth({ authenticated: true, token: 'token-1' })),
        provideAppApi(),
        provideHttpClient(withInterceptors(httpInterceptors)),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AUTH_PROVIDER) as FakeAuthProvider;
    notes = TestBed.inject(NotesApi);
    notifications = TestBed.inject(Notifications);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  it('builds versioned URLs that the bearer allowlist accepts', async () => {
    const result = firstValueFrom(notes.list());
    await settle();

    const request = http.expectOne('/api/v1/notes');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
    request.flush([]);
    await expect(result).resolves.toEqual([]);
  });

  it('keeps register unversioned and never retries it', async () => {
    const accounts = TestBed.inject(AccountApi);
    const result = firstValueFrom(
      accounts.register({
        username: 'bob',
        email: 'b@x.io',
        password: 'P@ssw0rd!',
        firstName: '',
        lastName: '',
      }),
    );
    result.catch(() => undefined);
    await settle();

    http.expectOne('/api/auth/register').flush(null, unavailable);
    await vi.advanceTimersByTimeAsync(10_000);

    http.expectNone('/api/auth/register');
    await expect(result).rejects.toMatchObject({ status: 503 });
  });

  it('signs every retried attempt and reports the failure once', async () => {
    const result = firstValueFrom(notes.list());
    result.catch(() => undefined);

    for (let attempt = 0; attempt <= 3; attempt++) {
      await vi.advanceTimersByTimeAsync(10_000);
      const request = http.expectOne('/api/v1/notes');
      expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
      request.flush({ status: 503, detail: 'Down for maintenance.' }, unavailable);
    }

    await expect(result).rejects.toMatchObject({ status: 503, detail: 'Down for maintenance.' });
    expect(notifications.toasts().map((t) => t.message)).toEqual(['Down for maintenance.']);
  });

  it('sends the user to sign in when the API answers 401 after the session ended', async () => {
    const result = firstValueFrom(notes.list());
    result.catch(() => undefined);
    await settle();

    auth.setAuthenticated(false); // the adapter's refresh was refused
    http
      .expectOne('/api/v1/notes')
      .flush(
        { status: 401, detail: 'You need to sign in.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    await settle();

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(auth.loginCalls).toHaveLength(1);
  });

  it('shows the API message for a 401 while the session is still valid', async () => {
    const result = firstValueFrom(notes.list());
    result.catch(() => undefined);
    await settle();

    http
      .expectOne('/api/v1/notes')
      .flush(
        { status: 401, detail: 'Not allowed here.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    await settle();

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(notifications.toasts().map((t) => t.message)).toEqual(['Not allowed here.']);
    expect(auth.loginCalls).toHaveLength(0);
  });

  it('reports an ended session once when several requests fail together', async () => {
    const results = [firstValueFrom(notes.list()), firstValueFrom(notes.list())];
    results.forEach((result) => result.catch(() => undefined));
    await settle();

    auth.setAuthenticated(false);
    for (const request of http.match('/api/v1/notes')) {
      request.flush(
        { status: 401, detail: 'You need to sign in.' },
        { status: 401, statusText: 'Unauthorized' },
      );
    }
    await settle();

    expect(notifications.toasts().map((t) => t.message)).toEqual([
      'Your session has ended. Please sign in again.',
    ]);
    expect(auth.loginCalls).toHaveLength(1);
  });

  it('leaves field errors to the form instead of toasting them', async () => {
    const result = firstValueFrom(notes.create(''));
    result.catch(() => undefined);
    await settle();

    http.expectOne('/api/v1/notes').flush(
      {
        status: 400,
        title: 'Bad Request',
        code: 'VALIDATION_ERROR',
        errors: [{ field: 'text', message: 'Required.' }],
      },
      { status: 400, statusText: 'Bad Request' },
    );

    const error = (await result.catch((e: unknown) => e)) as ApiError;
    expect(error.errors).toEqual([{ field: 'text', message: 'Required.' }]);
    expect(notifications.toasts()).toEqual([]);
  });
});
