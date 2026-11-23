// SPDX-License-Identifier: Apache-2.0
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  Injector,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { findFragment } from '../../../core/documents/text-segments';
import { MatButtonModule } from '@angular/material/button';
import { KeyFactStatus, MeaningVerification } from '../../../core/documents/document.models';
import { KEY_FACT_STATUS_LABELS, KEY_FACT_TYPE_LABELS, REVIEW_TEXTS } from '../../../core/ui-texts';

/**
 * Datos clave del original y su estado en la adaptación. Los avisos ("revisar" u "omitido") se destacan con su motivo
 * y se quedan a la vista aunque se marquen como revisados. Si el valor está en el original, se puede señalar allí. Con
 * dos avisos o más se pueden marcar todos de una vez, después de confirmarlo.
 */
@Component({
  selector: 'app-key-facts-panel',
  imports: [MatButtonModule],
  templateUrl: './key-facts-panel.html',
  styleUrl: '../panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KeyFactsPanelComponent {
  readonly verification = input.required<MeaningVerification | null>();
  readonly editable = input(false);
  readonly saving = input(false);
  readonly acknowledge = output<number>();
  /** Pide marcar como revisados todos los avisos (ya confirmado). */
  readonly acknowledgeAll = output<void>();
  /** Texto original, para saber qué datos se pueden señalar en él. */
  readonly originalText = input('');
  /** Dato clave señalado ahora en el original, o `null`. */
  readonly located = input<number | null>(null);
  /** Pide señalar un dato en el original (o quitar la señal, si ya lo estaba). */
  readonly locate = output<number>();

  protected readonly texts = REVIEW_TEXTS;
  protected readonly statusLabels = KEY_FACT_STATUS_LABELS;
  protected readonly typeLabels = KEY_FACT_TYPE_LABELS;

  /** Avisos sin marcar como revisados. */
  protected readonly pendingCount = computed(
    () => this.verification()?.keyFacts.filter((fact) => fact.requiresAcknowledgement).length ?? 0,
  );
  /** Se está pidiendo confirmación para marcar todos. */
  protected readonly confirming = signal(false);

  private readonly askAllButton = viewChild('askAllButton', { read: ElementRef<HTMLElement> });
  private readonly cancelButton = viewChild('cancelButton', { read: ElementRef<HTMLElement> });
  private readonly injector = inject(Injector);

  protected askAll(): void {
    this.confirming.set(true);
    // El foco va a "Cancelar": lo seguro es no marcar nada.
    this.focusAfterRender(() => this.cancelButton());
  }

  protected cancelAll(): void {
    this.confirming.set(false);
    this.focusAfterRender(() => this.askAllButton());
  }

  protected confirmAll(): void {
    this.confirming.set(false);
    this.acknowledgeAll.emit();
  }

  private focusAfterRender(target: () => ElementRef<HTMLElement> | undefined): void {
    afterNextRender(() => target()?.nativeElement.focus(), { injector: this.injector });
  }

  protected canLocate(value: string): boolean {
    return findFragment(this.originalText(), value) !== null;
  }

  /** Tono de la etiqueta de estado. Solo refuerza el texto: el estado siempre se escribe. */
  protected factTone(status: KeyFactStatus): 'ok' | 'pending' | 'danger' {
    return status === 'Conserved' ? 'ok' : status === 'Review' ? 'pending' : 'danger';
  }
}
