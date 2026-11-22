// SPDX-License-Identifier: Apache-2.0
import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

/** Tema de la interfaz. "system" sigue el tema claro u oscuro del sistema operativo. */
export type ThemeMode = 'system' | 'light' | 'dark';

/** Clave del almacenamiento local donde se recuerda el tema elegido (solo una preferencia de vista). */
export const THEME_KEY = 'lf.tema';

/** Orden en el que el botón compacto pasa de un tema al siguiente. */
export const THEME_ORDER: readonly ThemeMode[] = ['system', 'light', 'dark'];

/**
 * Tema claro, oscuro o el del sistema. Los colores usan light-dark(), que sigue a `color-scheme`: basta con marcar
 * el elemento raíz con `data-theme` y la hoja de estilos fija `color-scheme`. Sin marca, se sigue al sistema.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly root = inject(DOCUMENT).documentElement;
  private readonly current = signal<ThemeMode>(readMode());
  readonly mode = this.current.asReadonly();

  constructor() {
    this.apply(this.current());
  }

  set(mode: ThemeMode): void {
    this.current.set(mode);
    this.apply(mode);
    try {
      if (mode === 'system') {
        localStorage.removeItem(THEME_KEY);
      } else {
        localStorage.setItem(THEME_KEY, mode);
      }
    } catch {
      // Sin almacenamiento (modo privado, bloqueado): se recuerda solo mientras dure la página.
    }
  }

  /** Pasa al tema siguiente: sistema → claro → oscuro → sistema. */
  next(): void {
    const index = THEME_ORDER.indexOf(this.current());
    this.set(THEME_ORDER[(index + 1) % THEME_ORDER.length]);
  }

  private apply(mode: ThemeMode): void {
    if (mode === 'system') {
      this.root.removeAttribute('data-theme');
    } else {
      this.root.setAttribute('data-theme', mode);
    }
  }
}

function readMode(): ThemeMode {
  try {
    const stored = localStorage.getItem(THEME_KEY);
    return stored === 'light' || stored === 'dark' ? stored : 'system';
  } catch {
    return 'system';
  }
}
