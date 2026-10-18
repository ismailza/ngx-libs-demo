import { ErrorHandler, Injectable } from '@angular/core';
import { wasReported } from '../api/api-handlers';

/**
 * Angular's `ErrorHandler` also receives unhandled promise rejections
 * (`provideBrowserGlobalErrorListeners()`). An `ApiError` that
 * `ToastApiErrorHandler` already showed needs nothing more, so pages can
 * `await` an API call without wrapping it in an empty try/catch.
 */
@Injectable()
export class AppErrorHandler extends ErrorHandler {
  override handleError(error: unknown): void {
    if (!wasReported(error)) {
      super.handleError(error);
    }
  }
}
