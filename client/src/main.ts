import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { createAppConfig } from './app/app.config';
import { loadRuntimeConfig } from './app/core/config/runtime-config';

loadRuntimeConfig()
  .then((config) => bootstrapApplication(App, createAppConfig(config)))
  .catch((error: unknown) => {
    console.error(error);
    // Angular never started, so report it with plain DOM.
    const message = document.createElement('p');
    message.setAttribute('role', 'alert');
    message.className = 'startup-error';
    message.textContent =
      'The app could not start: the API is unreachable or misconfigured. Check that the server is running, then reload.';
    document.body.replaceChildren(message);
  });
