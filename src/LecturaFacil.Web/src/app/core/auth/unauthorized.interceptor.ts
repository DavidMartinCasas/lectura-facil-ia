// SPDX-License-Identifier: Apache-2.0
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { LOGIN_PATH, RETURN_URL_PARAM } from './auth.guard';
import { AUTH_PATH, AuthService } from './auth.service';

/**
 * Un 401 de la API significa que no hay sesión: se marca como cerrada y se lleva al login. Las rutas de /api/auth se
 * excluyen: el 401 del login es "credenciales incorrectas" y el de /me solo responde si hay sesión.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !request.url.startsWith(AUTH_PATH)
      ) {
        auth.sessionExpired();
        const current = router.url;
        if (!current.startsWith(LOGIN_PATH)) {
          void router.navigate([LOGIN_PATH], { queryParams: { [RETURN_URL_PARAM]: current } });
        }
      }
      return throwError(() => error);
    }),
  );
};
