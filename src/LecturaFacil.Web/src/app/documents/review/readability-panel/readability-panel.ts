// SPDX-License-Identifier: Apache-2.0
import { DecimalPipe } from '@angular/common';
import { booleanAttribute, ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReadabilityComparison } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';

/** Índice INFLESZ del original y de la adaptación, con su grado en palabras. */
@Component({
  selector: 'app-readability-panel',
  imports: [DecimalPipe],
  templateUrl: './readability-panel.html',
  styleUrls: ['../panel.scss', './readability-panel.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.compact]': 'compact()' },
})
export class ReadabilityPanelComponent {
  readonly readability = input.required<ReadabilityComparison | null>();
  /** Versión compacta, para la cabecera del documento: lo mismo en menos alto. */
  readonly compact = input(false, { transform: booleanAttribute });

  protected readonly texts = REVIEW_TEXTS;

  /** Posición en la escala INFLESZ (0 – 100) para el dibujo; el valor exacto se escribe aparte. */
  protected clamp(value: number): number {
    return Math.min(100, Math.max(0, value));
  }
}
