// SPDX-License-Identifier: Apache-2.0
import { PROGRESS_TEXTS } from '../ui-texts';
import { DocumentStatus } from './document.models';

export type StepState = 'done' | 'current' | 'pending' | 'failed';

export interface ProgressStep {
  readonly label: string;
  readonly state: StepState;
}

const LABELS = [
  PROGRESS_TEXTS.steps.received,
  PROGRESS_TEXTS.steps.adapting,
  PROGRESS_TEXTS.steps.reviewing,
  PROGRESS_TEXTS.steps.verifying,
  PROGRESS_TEXTS.steps.ready,
] as const;

const ADAPTING_STEP = 1;

/**
 * Pasos que se muestran para un estado. La API solo distingue "en cola" (Received) y "adaptando" (Processing): la
 * revisión de pautas y la verificación ocurren dentro de Processing, así que se marcan como hechas al terminar.
 * Si falla, se marca con error el paso de adaptación, que es el único que se sabe que estaba en curso.
 */
export function progressSteps(status: DocumentStatus): readonly ProgressStep[] {
  switch (status) {
    case 'Received':
      return withCurrent(0, 'current');
    case 'Processing':
      return withCurrent(ADAPTING_STEP, 'current');
    case 'Error':
      return withCurrent(ADAPTING_STEP, 'failed');
    default:
      return LABELS.map((label) => ({ label, state: 'done' }));
  }
}

function withCurrent(index: number, state: 'current' | 'failed'): readonly ProgressStep[] {
  return LABELS.map((label, position) => ({
    label,
    state: position < index ? 'done' : position === index ? state : 'pending',
  }));
}
