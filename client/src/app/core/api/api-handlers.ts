import { effect, inject, Injectable } from '@angular/core';
import {
  type ApiError,
  ApiErrorHandler,
  ApiSuccessHandler,
  isApiError,
} from '@ismailza/ngx-api-client';
import { AuthService } from '@ismailza/ngx-auth-client';
import { Notifications } from '../notifications/notifications';

const reported = new WeakSet<ApiError>();

/** True when `ToastApiErrorHandler` has already dealt with this error. */
export const wasReported = (error: unknown): boolean => isApiError(error) && reported.has(error);

/**
 * Where the two libraries meet: ngx-api-client reports the failure, and
 * ngx-auth-client answers the session question it raises.
 */
@Injectable()
export class ToastApiErrorHandler extends ApiErrorHandler {
  private readonly notifications = inject(Notifications);
  private readonly auth = inject(AuthService);
  /** One "session ended" toast per session, however many requests fail with it. */
  private sessionEndReported = false;

  constructor() {
    super();
    effect(() => {
      if (this.auth.authenticated()) {
        this.sessionEndReported = false;
      }
    });
  }

  override handle(error: ApiError): void {
    // The rejection still reaches the caller; AppErrorHandler then ignores it.
    reported.add(error);

    // Field errors are shown next to the fields by the form that sent them, so every
    // request with a body must come from a form that maps `error.errors` to its fields.
    if (error.code === 'VALIDATION_ERROR' && error.errors?.length) {
      return;
    }

    if (error.status === 401 && !this.auth.authenticated()) {
      // The session could not be refreshed (revoked or expired): the adapter
      // has already signed out, so send the user to sign in again — once, even
      // when several requests fail together.
      if (!this.sessionEndReported) {
        this.sessionEndReported = true;
        this.notifications.error('Your session has ended. Please sign in again.');
        void this.auth.login();
      }
      return;
    }

    // Includes a 401 while still signed in: the session is fine, so show what the API said.
    this.notifications.error(error.detail, error.traceId ? `Trace id ${error.traceId}` : undefined);
  }
}

@Injectable()
export class ToastApiSuccessHandler extends ApiSuccessHandler {
  private readonly notifications = inject(Notifications);

  override handle(message: string): void {
    this.notifications.success(message);
  }
}
