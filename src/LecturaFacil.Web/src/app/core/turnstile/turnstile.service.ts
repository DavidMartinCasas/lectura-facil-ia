// SPDX-License-Identifier: Apache-2.0
import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';

/** Script oficial de Cloudflare Turnstile, en modo de renderizado explícito. Debe estar permitido en la CSP. */
export const TURNSTILE_SCRIPT_URL =
  'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

/** Opciones de `turnstile.render` que se usan aquí. */
export interface TurnstileRenderOptions {
  readonly sitekey: string;
  readonly language?: string;
  readonly callback: (token: string) => void;
  readonly 'expired-callback': () => void;
  readonly 'error-callback': () => void;
}

/** La parte de la API global `window.turnstile` que se usa. */
export interface TurnstileApi {
  render(container: HTMLElement, options: TurnstileRenderOptions): string;
  reset(widgetId: string): void;
  remove(widgetId: string): void;
}

type WindowWithTurnstile = Window & { turnstile?: TurnstileApi };

/**
 * Carga el script de Turnstile solo cuando hace falta (desafío habilitado y un formulario en pantalla), una sola vez.
 * Con Turnstile deshabilitado nunca se descarga nada de Cloudflare.
 */
@Injectable({ providedIn: 'root' })
export class TurnstileService {
  private readonly document = inject(DOCUMENT);
  private loading: Promise<TurnstileApi> | null = null;

  load(): Promise<TurnstileApi> {
    this.loading ??= new Promise<TurnstileApi>((resolve, reject) => {
      const view = this.document.defaultView as WindowWithTurnstile | null;
      if (view?.turnstile) {
        resolve(view.turnstile);
        return;
      }
      const script = this.document.createElement('script');
      script.src = TURNSTILE_SCRIPT_URL;
      script.async = true;
      script.onload = () =>
        view?.turnstile ? resolve(view.turnstile) : reject(new Error('Turnstile no disponible'));
      script.onerror = () => {
        this.loading = null;
        reject(new Error('No se ha podido cargar Turnstile'));
      };
      this.document.head.appendChild(script);
    });
    return this.loading;
  }
}
