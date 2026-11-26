// SPDX-License-Identifier: Apache-2.0
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';

export const AUTH_PATH = '/api/auth';

/** Usuario con sesión, tal como lo devuelve /api/auth/me. */
export interface CurrentUser {
  readonly email: string;
}

/**
 * Estado de la sesión. La sesión es una cookie HttpOnly del backend: aquí solo se sabe quién ha entrado, nunca se
 * guarda ningún token. El token antiforgery lo envía Angular solo (cookie XSRF-TOKEN → cabecera X-XSRF-TOKEN).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  /** `undefined` mientras no se ha preguntado al servidor; `null` sin sesión. */
  private readonly current = signal<CurrentUser | null | undefined>(undefined);

  readonly user = this.current.asReadonly();
  readonly isAuthenticated = computed(() => !!this.current());

  /** Pregunta en curso a /me: quien llama mientras tanto (la barra al arrancar, el guard) comparte la misma. */
  private pending: Observable<boolean> | undefined;

  /** Pregunta al servidor si hay sesión. De paso, el servidor deja la cookie con el token antiforgery. */
  loadSession(): Observable<boolean> {
    this.pending ??= this.http.get<CurrentUser>(`${AUTH_PATH}/me`).pipe(
      tap((user) => this.current.set(user)),
      map(() => true),
      catchError(() => {
        this.current.set(null);
        return of(false);
      }),
      finalize(() => (this.pending = undefined)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.pending;
  }

  login(email: string, password: string): Observable<CurrentUser> {
    return this.http
      .post<CurrentUser>(`${AUTH_PATH}/login`, { email, password })
      .pipe(tap((user) => this.current.set(user)));
  }

  /**
   * Cierra la sesión en el servidor. Solo se da por cerrada si el servidor lo confirma: si falla, la cookie HttpOnly
   * sigue valiendo y el error llega a quien llama para que lo diga y se pueda reintentar.
   */
  logout(): Observable<void> {
    return this.http.post<void>(`${AUTH_PATH}/logout`, null).pipe(
      map(() => undefined),
      tap(() => this.current.set(null)),
    );
  }

  /** La API ha respondido 401: la sesión ha caducado o se ha cerrado en otra pestaña. */
  sessionExpired(): void {
    this.current.set(null);
  }
}
