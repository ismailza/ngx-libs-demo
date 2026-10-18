import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideAuth } from '@ismailza/ngx-auth-client';
import { routes } from './app.routes';
import { httpInterceptors, provideAppApi } from './core/api/http';
import { authFeatureFor } from './core/auth/auth-feature';
import { RUNTIME_CONFIG, type RuntimeConfig } from './core/config/runtime-config';
import { AppErrorHandler } from './core/errors/app-error-handler';

/** Built from the runtime config, which `main.ts` loads before bootstrap. */
export function createAppConfig(runtime: RuntimeConfig): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      // Skips API errors already shown as a toast, so pages need no empty catch blocks.
      { provide: ErrorHandler, useClass: AppErrorHandler },
      { provide: RUNTIME_CONFIG, useValue: runtime },
      // ngx-auth-client: the session. The adapter follows the server's provider.
      provideAuth(authFeatureFor(runtime.auth)),
      // ngx-api-client: every other API call.
      provideAppApi(),
      provideHttpClient(withInterceptors(httpInterceptors)),
      provideRouter(routes, withComponentInputBinding()),
    ],
  };
}
