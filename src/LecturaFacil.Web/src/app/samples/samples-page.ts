// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Router } from '@angular/router';
import { PublicConfigService } from '../core/config/public-config.service';
import { DocumentSubmissionStore } from '../core/documents/document-submission.store';
import { SampleText, SamplesApiService } from '../core/samples/samples-api.service';
import { APP_TEXTS, PROGRESS_TEXTS, SAMPLES_TEXTS, TURNSTILE_TEXTS } from '../core/ui-texts';
import { ProgressStepsComponent } from '../documents/progress-steps/progress-steps';
import { TurnstileWidgetComponent } from '../documents/turnstile-widget/turnstile-widget';

type LoadState = 'loading' | 'loaded' | 'failed';

/**
 * Pantalla "Ejemplos" (US-16): lista los textos de ejemplo y lanza la adaptación de cualquiera con un clic. Es un envío
 * normal de texto, con el mismo progreso, los mismos límites y el mismo desafío anti-bots que "Nuevo documento".
 */
@Component({
  selector: 'app-samples-page',
  imports: [MatButtonModule, MatCardModule, ProgressStepsComponent, TurnstileWidgetComponent],
  providers: [DocumentSubmissionStore],
  templateUrl: './samples-page.html',
  styleUrls: ['../documents/pages/page.scss', './samples-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SamplesPage {
  protected readonly store = inject(DocumentSubmissionStore);
  private readonly api = inject(SamplesApiService);
  private readonly router = inject(Router);

  protected readonly texts = SAMPLES_TEXTS;
  protected readonly progressTexts = PROGRESS_TEXTS;
  protected readonly draftNotice = APP_TEXTS.draftNotice;
  protected readonly turnstileSiteKey = inject(PublicConfigService).turnstileSiteKey;
  protected readonly samples = signal<readonly SampleText[]>([]);
  protected readonly state = signal<LoadState>('loading');
  protected readonly humanError = signal<string | null>(null);
  private readonly humanToken = signal<string | null>(null);
  private readonly turnstile = viewChild(TurnstileWidgetComponent);

  constructor() {
    this.load();
    effect(() => {
      const id = this.store.documentId();
      if (this.store.phase() === 'ready' && id !== null) {
        void this.router.navigate(['/documentos', id]);
      }
    });
  }

  protected load(): void {
    this.state.set('loading');
    this.api.list().subscribe({
      next: (samples) => {
        this.samples.set(samples);
        this.state.set('loaded');
      },
      error: () => this.state.set('failed'),
    });
  }

  protected onHumanToken(token: string | null): void {
    this.humanToken.set(token);
    if (token !== null) {
      this.humanError.set(null);
    }
  }

  protected adapt(sample: SampleText): void {
    if (this.store.phase() === 'submitting' || this.store.phase() === 'tracking') {
      return;
    }
    const token = this.humanToken();
    if (this.turnstileSiteKey() !== null && token === null) {
      this.humanError.set(TURNSTILE_TEXTS.required);
      return;
    }
    this.humanError.set(null);
    this.store.submit({
      kind: 'text',
      text: sample.text,
      title: sample.title,
      ...(token === null ? {} : { humanToken: token }),
    });
    if (token !== null) {
      this.turnstile()?.reset();
    }
  }
}
