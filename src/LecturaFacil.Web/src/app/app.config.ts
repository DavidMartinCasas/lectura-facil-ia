// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { problemDetailsInterceptor } from './core/errors/problem-details.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // La sesión (US-15) irá en cookies HttpOnly que gestiona el navegador: aquí no se guarda ningún token.
    provideHttpClient(withInterceptors([problemDetailsInterceptor])),
  ],
};
