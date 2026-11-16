// SPDX-License-Identifier: Apache-2.0
import { TurnstileApi, TurnstileRenderOptions } from '../core/turnstile/turnstile.service';

/** Sustituye a Cloudflare en los tests: guarda las opciones del widget para poder resolver el desafío. */
export class FakeTurnstile implements TurnstileApi {
  options: TurnstileRenderOptions | null = null;
  resets = 0;
  removed = 0;

  render(_container: HTMLElement, options: TurnstileRenderOptions): string {
    this.options = options;
    return 'widget-1';
  }

  reset(): void {
    this.resets++;
  }

  remove(): void {
    this.removed++;
  }

  /** Simula que la persona supera el desafío. */
  solve(token: string): void {
    this.options?.callback(token);
  }
}
