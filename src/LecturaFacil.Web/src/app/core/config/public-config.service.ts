// SPDX-License-Identifier: Apache-2.0
import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';

export const PUBLIC_CONFIG_PATH = '/api/config/public';

/** Respuesta de GET /api/config/public: solo datos públicos (US-16). */
export interface PublicConfig {
  readonly notice: string;
  readonly turnstileEnabled: boolean;
  readonly turnstileSiteKey: string;
}

const DEFAULT_CONFIG: PublicConfig = { notice: '', turnstileEnabled: false, turnstileSiteKey: '' };

/**
 * Configuración pública de la interfaz: el aviso configurable y si hay que mostrar el desafío anti-bots. Se pide una vez
 * al arrancar. Si falla, la interfaz sigue sin aviso ni widget; el servidor valida igualmente el desafío si está activo.
 */
@Injectable({ providedIn: 'root' })
export class PublicConfigService {
  private readonly http = inject(HttpClient);
  private readonly config = signal<PublicConfig>(DEFAULT_CONFIG);
  private requested = false;

  readonly notice = computed(() => this.config().notice.trim());
  /** Clave del sitio de Turnstile, o `null` si el desafío está deshabilitado. */
  readonly turnstileSiteKey = computed(() => {
    const { turnstileEnabled, turnstileSiteKey } = this.config();
    return turnstileEnabled && turnstileSiteKey ? turnstileSiteKey : null;
  });

  load(): void {
    if (this.requested) {
      return;
    }
    this.requested = true;
    this.http.get<PublicConfig>(PUBLIC_CONFIG_PATH).subscribe({
      next: (config) => this.config.set({ ...DEFAULT_CONFIG, ...config }),
      error: () => this.config.set(DEFAULT_CONFIG),
    });
  }
}
