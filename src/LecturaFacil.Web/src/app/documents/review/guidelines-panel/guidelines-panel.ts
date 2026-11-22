// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { GuidelineReport, GuidelineStatus } from '../../../core/documents/document.models';
import { GUIDELINE_STATUS_LABELS, REVIEW_TEXTS } from '../../../core/ui-texts';

/** Pautas de lectura fácil que cumple la adaptación. El estado se dice con texto, no solo con color. */
@Component({
  selector: 'app-guidelines-panel',
  templateUrl: './guidelines-panel.html',
  styleUrl: '../panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GuidelinesPanelComponent {
  readonly report = input.required<GuidelineReport | null>();

  protected readonly texts = REVIEW_TEXTS;
  protected readonly statusLabels = GUIDELINE_STATUS_LABELS;
  protected readonly tones: Readonly<Record<GuidelineStatus, string>> = {
    Compliant: 'ok',
    NotCompliant: 'pending',
    NotChecked: 'neutral',
  };
}
