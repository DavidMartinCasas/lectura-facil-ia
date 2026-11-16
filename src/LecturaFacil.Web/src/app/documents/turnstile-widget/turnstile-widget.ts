// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TurnstileApi, TurnstileService } from '../../core/turnstile/turnstile.service';
import { TURNSTILE_TEXTS } from '../../core/ui-texts';

/**
 * Widget de Cloudflare Turnstile (US-16). Emite el token cuando la persona supera el desafío y `null` cuando caduca o
 * falla. El token es de un solo uso: quien lo usa llama a {@link reset} después de cada envío.
 */
@Component({
  selector: 'app-turnstile-widget',
  template: `
    <div class="turnstile">
      <p class="hint">{{ texts.hint }}</p>
      <div #container></div>
      @if (failed()) {
        <p class="error" role="alert">{{ texts.loadFailed }}</p>
      }
    </div>
  `,
  styles: `
    .turnstile {
      display: grid;
      gap: 0.5rem;
    }
    .hint,
    .error {
      margin: 0;
    }
    .error {
      color: var(--mat-sys-error);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TurnstileWidgetComponent {
  readonly siteKey = input.required<string>();
  readonly token = output<string | null>();

  protected readonly texts = TURNSTILE_TEXTS;
  protected readonly failed = signal(false);

  private readonly turnstile = inject(TurnstileService);
  private readonly container = viewChild.required<ElementRef<HTMLElement>>('container');
  private api: TurnstileApi | null = null;
  private widgetId: string | null = null;
  private destroyed = false;

  constructor() {
    afterNextRender(() => void this.render());
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      if (this.api && this.widgetId !== null) {
        this.api.remove(this.widgetId);
      }
    });
  }

  /** Pide un desafío nuevo (los tokens son de un solo uso). */
  reset(): void {
    this.token.emit(null);
    if (this.api && this.widgetId !== null) {
      this.api.reset(this.widgetId);
    }
  }

  private async render(): Promise<void> {
    try {
      const api = await this.turnstile.load();
      if (this.destroyed) {
        return;
      }
      this.api = api;
      this.widgetId = api.render(this.container().nativeElement, {
        sitekey: this.siteKey(),
        language: 'es',
        callback: (token) => this.token.emit(token),
        'expired-callback': () => this.token.emit(null),
        'error-callback': () => this.token.emit(null),
      });
    } catch {
      this.failed.set(true);
    }
  }
}
