// SPDX-License-Identifier: Apache-2.0
import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { Observable, map, of } from 'rxjs';
import { AuthService } from './auth.service';

export const LOGIN_PATH = '/entrar';
export const RETURN_URL_PARAM = 'volver';

/** Las pantallas con documentos exigen sesión: sin ella, se va al login y después se vuelve aquí. */
export const authGuard: CanActivateFn = (_route, state): Observable<boolean | UrlTree> => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toLogin = (): UrlTree =>
    router.createUrlTree([LOGIN_PATH], { queryParams: { [RETURN_URL_PARAM]: state.url } });

  if (auth.isAuthenticated()) {
    return of(true);
  }
  return auth.loadSession().pipe(map((authenticated) => authenticated || toLogin()));
};

/** Solo rutas internas de la aplicación: nunca una URL absoluta que lleve a otro sitio (open redirect). */
export function safeReturnUrl(value: string | null | undefined): string {
  return value && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\')
    ? value
    : '/documentos';
}
