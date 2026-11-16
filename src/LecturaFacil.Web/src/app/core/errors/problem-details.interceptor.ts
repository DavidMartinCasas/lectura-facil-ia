// SPDX-License-Identifier: Apache-2.0
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { toApiError } from './api-error';

/** Convierte cualquier error HTTP en un {@link ApiError} con un mensaje comprensible en español. */
export const problemDetailsInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) =>
      throwError(() =>
        error instanceof HttpErrorResponse
          ? toApiError(error.status, error.error)
          : toApiError(-1, null),
      ),
    ),
  );
