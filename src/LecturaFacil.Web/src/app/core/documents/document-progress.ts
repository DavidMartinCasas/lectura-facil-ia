// SPDX-License-Identifier: Apache-2.0
import { PROGRESS_TEXTS } from '../ui-texts';
import { DocumentStatus } from './document.models';

export type StepState = 'done' | 'current' | 'pending' | 'failed';

export interface ProgressStep {
  readonly label: string;
  readonly state: StepState;
  /** En el paso en curso de un documento largo, por qué parte va: "parte 3 de 10". */
  readonly detail?: string | null;
}

/** La parte de un documento largo por la que va una fase (US-23). */
export interface PartProgress {
  readonly number: number;
  readonly count: number;
}

/** Fase del proceso mientras el documento está en "Procesando". La envía el servidor por SSE. */
export const PROCESSING_STAGES = [
  'Adapting',
  'ReviewingGuidelines',
  'Retrying',
  'Verifying',
] as const;

export type ProcessingStage = (typeof PROCESSING_STAGES)[number];

export function isProcessingStage(value: unknown): value is ProcessingStage {
  return typeof value === 'string' && (PROCESSING_STAGES as readonly string[]).includes(value);
}

/** Lo que llega del servidor: el estado y, mientras se adapta, la fase y, si tiene varias partes, por cuál va. */
export interface DocumentProgress {
  readonly status: DocumentStatus;
  readonly stage: ProcessingStage | null;
  readonly part?: PartProgress | null;
}

/**
 * Dónde está la lista de pasos: el paso en curso (`READY` si todos están hechos), si es otra adaptación
 * tras incumplir pautas y si ha fallado.
 */
export interface ProgressPosition {
  readonly index: number;
  readonly retrying: boolean;
  readonly failed: boolean;
  readonly part?: PartProgress | null;
}

const LABELS = [
  PROGRESS_TEXTS.steps.received,
  PROGRESS_TEXTS.steps.adapting,
  PROGRESS_TEXTS.steps.reviewing,
  PROGRESS_TEXTS.steps.verifying,
  PROGRESS_TEXTS.steps.ready,
] as const;

const RECEIVED = 0;
const ADAPTING = 1;
const REVIEWING = 2;
const VERIFYING = 3;
const LAST = LABELS.length - 1;
/** Todos los pasos hechos, también "Listo". */
const READY = LABELS.length;

const STAGE_STEPS: Readonly<Record<ProcessingStage, number>> = {
  Adapting: ADAPTING,
  ReviewingGuidelines: REVIEWING,
  Retrying: ADAPTING,
  Verifying: VERIFYING,
};

/**
 * En qué paso está un documento. Mientras se procesa, la fase dice si está adaptando, revisando las pautas o
 * verificando; sin fase (un servidor antiguo o un evento sin ella), se supone que está adaptando. Si falla, se marca
 * con error el paso en el que estaba.
 */
export function progressPosition(
  status: DocumentStatus,
  stage: ProcessingStage | null = null,
  part: PartProgress | null = null,
): ProgressPosition {
  switch (status) {
    case 'Received':
      return at(RECEIVED);
    case 'Processing':
      return stage === null || part === null
        ? at(stage === null ? ADAPTING : STAGE_STEPS[stage], stage === 'Retrying')
        : { ...at(STAGE_STEPS[stage], stage === 'Retrying'), part };
    case 'Error':
      return { ...at(stage === null ? ADAPTING : STAGE_STEPS[stage]), failed: true };
    default:
      return at(READY);
  }
}

/** Los pasos que se ven en una posición. */
export function stepsAt(position: ProgressPosition): readonly ProgressStep[] {
  const part = position.part ?? null;
  return LABELS.map((label, index) => {
    const state: StepState =
      index < position.index
        ? 'done'
        : index > position.index
          ? 'pending'
          : position.failed
            ? 'failed'
            : 'current';
    const step: ProgressStep = {
      label: index === ADAPTING && position.retrying ? PROGRESS_TEXTS.steps.adaptingAgain : label,
      state,
    };
    return state === 'current' && part !== null
      ? { ...step, detail: PROGRESS_TEXTS.part(part.number, part.count, position.retrying) }
      : step;
  });
}

/** Pasos que se muestran para un estado y una fase. */
export function progressSteps(
  status: DocumentStatus,
  stage: ProcessingStage | null = null,
): readonly ProgressStep[] {
  return stepsAt(progressPosition(status, stage));
}

/**
 * El siguiente paso que se enseña camino de `target`, o `null` si ya se está ahí. Hacia delante se avanza de
 * uno en uno, siempre en orden, para que se vean también las fases que el servidor ha hecho muy deprisa; "Listo"
 * no se muestra "en curso": de "Verificando" se pasa a todo hecho. Un error o una nueva adaptación (que vuelve
 * atrás) se enseñan tal cual.
 */
export function nextPosition(
  shown: ProgressPosition,
  target: ProgressPosition,
): ProgressPosition | null {
  if (samePosition(shown, target)) {
    return null;
  }
  if (target.failed || target.index <= shown.index) {
    return target;
  }
  const index = shown.index + 1 === LAST ? READY : shown.index + 1;
  return index >= target.index ? target : at(index);
}

export function isReady(position: ProgressPosition): boolean {
  return position.index === READY && !position.failed;
}

function samePosition(a: ProgressPosition, b: ProgressPosition): boolean {
  return (
    a.index === b.index &&
    a.retrying === b.retrying &&
    a.failed === b.failed &&
    a.part?.number === b.part?.number &&
    a.part?.count === b.part?.count
  );
}

function at(index: number, retrying = false): ProgressPosition {
  return { index, retrying, failed: false };
}
