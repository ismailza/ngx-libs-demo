import { HttpInterceptorFn } from '@angular/common/http';
import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import {
  ApiErrorHandler,
  apiErrorInterceptor,
  ApiSuccessHandler,
  apiSuccessInterceptor,
  provideApi,
  retryInterceptor,
} from '@ismailza/ngx-api-client';
import { authTokenInterceptor } from '@ismailza/ngx-auth-client';
import { ToastApiErrorHandler, ToastApiSuccessHandler } from './api-handlers';

/**
 * ngx-api-client for every call except auth. `baseUrl: ''` keeps URLs relative
 * (`/api/v1/notes`), which is what authTokenInterceptor's default `/api`
 * allowlist matches — an absolute base URL needs a matching `bearer.urlPattern`.
 */
export const provideAppApi = (): EnvironmentProviders =>
  makeEnvironmentProviders([
    provideApi({
      baseUrl: '',
      prefix: 'api',
      version: 1,
      retry: { maxRetries: 3, initialDelay: 500 },
      defaultSuccessMessages: { post: 'Saved.', delete: 'Deleted.' },
    }),
    // After provideApi(), which registers the defaults.
    { provide: ApiErrorHandler, useClass: ToastApiErrorHandler },
    { provide: ApiSuccessHandler, useClass: ToastApiSuccessHandler },
  ]);

/** First is outermost. This order is what makes the two libraries cooperate. */
export const httpInterceptors: HttpInterceptorFn[] = [
  // Turns the final failure into one ApiError, reported once per operation.
  apiErrorInterceptor,
  apiSuccessInterceptor,
  // Retries the raw HttpErrorResponse, so it must sit inside apiErrorInterceptor.
  retryInterceptor,
  // Innermost (ngx-auth-client): every retried attempt is signed with a fresh token.
  authTokenInterceptor,
];
