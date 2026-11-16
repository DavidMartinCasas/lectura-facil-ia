// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { ApplicationConfig, ErrorHandler, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { createErrorHandler } from '@sentry/angular';
import { routes } from './app.routes';
import { unauthorizedInterceptor } from './core/auth/unauthorized.interceptor';
import { problemDetailsInterceptor } from './core/errors/problem-details.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Los errores no controlados van a Sentry si está activo (US-17); si no, solo a la consola, como antes.
    { provide: ErrorHandler, useValue: createErrorHandler() },
    provideRouter(routes, withComponentInputBinding()),
    // La sesión es una cookie HttpOnly que gestiona el navegador: aquí no se guarda ningún token. El token antiforgery
    // va de la cookie XSRF-TOKEN a la cabecera X-XSRF-TOKEN en las peticiones que cambian algo (doble envío).
    // unauthorizedInterceptor va después para ver el error HTTP original, antes de traducirlo.
    provideHttpClient(
      withInterceptors([problemDetailsInterceptor, unauthorizedInterceptor]),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
  ],
};
