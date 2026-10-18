import { ErrorHandler } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { type ApiError, ApiErrorHandler } from '@ismailza/ngx-api-client';
import { provideAuth } from '@ismailza/ngx-auth-client';
import { withFakeAuth } from '@ismailza/ngx-auth-client/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { provideAppApi } from '../api/http';
import { AppErrorHandler } from './app-error-handler';

const apiError = (): ApiError => ({
  type: 'about:blank',
  status: 503,
  code: 'EXTERNAL_SERVICE_ERROR',
  detail: 'The service is temporarily unavailable.',
  timestamp: '2026-10-18T00:00:00Z',
});

describe('AppErrorHandler', () => {
  let errorHandler: ErrorHandler;
  let consoleError: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideAuth(withFakeAuth({ authenticated: true })),
        provideAppApi(),
        { provide: ErrorHandler, useClass: AppErrorHandler },
      ],
    });
    errorHandler = TestBed.inject(ErrorHandler);
    consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  it('ignores an ApiError the toast handler already reported', () => {
    const error = apiError();
    TestBed.inject(ApiErrorHandler).handle(error);

    errorHandler.handleError(error);

    expect(consoleError).not.toHaveBeenCalled();
  });

  it('still logs an ApiError nobody reported (skipErrorHandler)', () => {
    errorHandler.handleError(apiError());

    expect(consoleError).toHaveBeenCalled();
  });

  it('still logs everything else', () => {
    errorHandler.handleError(new Error('bug'));

    expect(consoleError).toHaveBeenCalled();
  });
});
