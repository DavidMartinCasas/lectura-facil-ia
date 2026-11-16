// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { createErrorHandler } from '@sentry/angular';
import { routes } from './app.routes';
import { problemDetailsInterceptor } from './core/errors/problem-details.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Los errores no controlados van a Sentry si está activo (US-17); si no, solo a la consola, como antes.
    { provide: ErrorHandler, useValue: createErrorHandler() },
    provideRouter(routes, withComponentInputBinding()),
    // La sesión (US-15) irá en cookies HttpOnly que gestiona el navegador: aquí no se guarda ningún token.
    provideHttpClient(withInterceptors([problemDetailsInterceptor])),
  ],
};
