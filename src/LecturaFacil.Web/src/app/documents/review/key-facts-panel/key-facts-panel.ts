// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { KeyFactStatus, MeaningVerification } from '../../../core/documents/document.models';
import { KEY_FACT_STATUS_LABELS, KEY_FACT_TYPE_LABELS, REVIEW_TEXTS } from '../../../core/ui-texts';

/**
 * Datos clave del original y su estado en la adaptación. Los avisos ("revisar" u "omitido") se destacan con su motivo
 * y se quedan a la vista aunque se marquen como revisados.
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

  protected readonly texts = REVIEW_TEXTS;
  protected readonly statusLabels = KEY_FACT_STATUS_LABELS;
  protected readonly typeLabels = KEY_FACT_TYPE_LABELS;

  /** Tono de la etiqueta de estado. Solo refuerza el texto: el estado siempre se escribe. */
  protected factTone(status: KeyFactStatus): 'ok' | 'pending' | 'danger' {
    return status === 'Conserved' ? 'ok' : status === 'Review' ? 'pending' : 'danger';
  }
}
