// SPDX-License-Identifier: Apache-2.0
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Observable, catchError, from, map, of, switchMap, throwError } from 'rxjs';
import { toApiError } from './api-error';

/** Convierte cualquier error HTTP en un {@link ApiError} con un mensaje comprensible en español. */
export const problemDetailsInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) =>
      error instanceof HttpErrorResponse
        ? readBody(error.error).pipe(
            switchMap((body) => throwError(() => toApiError(error.status, body))),
          )
        : throwError(() => toApiError(-1, null)),
    ),
  );

/**
 * En las descargas (responseType "blob") el ProblemDetails de un error llega como Blob: se lee como JSON para conocer su
 * código. Si no es JSON, se trata como un cuerpo sin código.
 */
function readBody(body: unknown): Observable<unknown> {
  if (!(body instanceof Blob)) {
    return of(body);
  }
  return from(body.text()).pipe(
    map((text): unknown => JSON.parse(text)),
    catchError(() => of(null)),
  );
}
