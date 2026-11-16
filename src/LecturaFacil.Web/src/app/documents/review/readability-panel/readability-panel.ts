// SPDX-License-Identifier: Apache-2.0
import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReadabilityComparison } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';

/** Índice INFLESZ del original y de la adaptación, con su grado en palabras. */
@Component({
  selector: 'app-readability-panel',
  imports: [DecimalPipe],
  templateUrl: './readability-panel.html',
  styleUrls: ['../panel.scss', './readability-panel.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReadabilityPanelComponent {
  readonly readability = input.required<ReadabilityComparison | null>();

  protected readonly texts = REVIEW_TEXTS;
}
