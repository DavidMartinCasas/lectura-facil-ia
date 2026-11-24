// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProgressStep, StepState } from '../../core/documents/document-progress';
import { PROGRESS_TEXTS } from '../../core/ui-texts';

const STATE_LABELS: Readonly<Record<StepState, string>> = {
  done: PROGRESS_TEXTS.stateDone,
  current: PROGRESS_TEXTS.stateCurrent,
  pending: PROGRESS_TEXTS.statePending,
  failed: PROGRESS_TEXTS.stateFailed,
};

const MARKERS: Readonly<Record<StepState, string>> = {
  done: '✓',
  current: '',
  pending: '',
  failed: '✕',
};

/** Lista de pasos del proceso. El estado de cada paso se dice con texto, no solo con color o iconos. */
@Component({
  selector: 'app-progress-steps',
  templateUrl: './progress-steps.html',
  styleUrl: './progress-steps.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProgressStepsComponent {
  readonly steps = input.required<readonly ProgressStep[]>();
  /** Sin título visible cuando ya lo pone quien lo usa (el panel de "Nuevo documento"). Se sigue leyendo en voz alta. */
  readonly showTitle = input(true);

  protected readonly texts = PROGRESS_TEXTS;
  protected readonly stateLabels = STATE_LABELS;
  protected readonly markers = MARKERS;

  /** Lo que se anuncia a los lectores de pantalla cada vez que cambia el paso. */
  protected readonly announcement = computed(() => {
    const steps = this.steps();
    if (steps.some((step) => step.state === 'failed')) {
      return PROGRESS_TEXTS.announceFailed;
    }
    const current = steps.find((step) => step.state === 'current');
    return current
      ? `${PROGRESS_TEXTS.announceCurrent} ${current.label}${current.detail ? ' ' + current.detail : ''}.`
      : PROGRESS_TEXTS.announceReady;
  });
}
