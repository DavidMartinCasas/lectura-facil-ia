// SPDX-License-Identifier: Apache-2.0
import { DestroyRef, Injectable, InjectionToken, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS, PROGRESS_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import {
  DocumentProgress,
  ProgressPosition,
  isReady,
  nextPosition,
  progressPosition,
  stepsAt,
} from './document-progress';
import { DocumentStatus, DocumentSubmission, isAwaitingAdaptation } from './document.models';
import { DocumentsApiService } from './documents-api.service';

export type SubmissionPhase = 'idle' | 'submitting' | 'tracking' | 'ready' | 'failed';

/**
 * Tiempo mínimo que se ve cada paso del progreso, en milisegundos. Algunas fases duran muy poco en el servidor:
 * sin este mínimo, los pasos parpadearían o se saltarían. Se puede poner a 0 en los tests.
 */
export const PROGRESS_STEP_MS = new InjectionToken<number>('PROGRESS_STEP_MS', {
  providedIn: 'root',
  factory: () => 650,
});

/**
 * Estado del envío de un documento en la pantalla "Nuevo documento": envía el texto o el PDF, sigue su progreso por
 * SSE y guarda el último envío para poder reintentarlo. Se provee en el componente de la página.
 * Los pasos avanzan de uno en uno y en orden, cada uno a la vista al menos `PROGRESS_STEP_MS`; el envío solo
 * queda "listo" cuando el servidor ha terminado y la lista ha llegado al final.
 */
@Injectable()
export class DocumentSubmissionStore {
  private readonly api = inject(DocumentsApiService);
  private readonly events = inject(DocumentEventsService);
  private readonly stepMs = inject(PROGRESS_STEP_MS);

  private readonly _phase = signal<SubmissionPhase>('idle');
  private readonly _status = signal<DocumentStatus | null>(null);
  private readonly _documentId = signal<string | null>(null);
  private readonly _error = signal<string | null>(null);
  /** Los pasos que se ven, que van detrás de lo que dice el servidor (`target`) como mucho un paso cada vez. */
  private readonly _shown = signal<ProgressPosition | null>(null);
  private target: ProgressPosition | null = null;
  private shownSince = 0;
  private stepTimer: ReturnType<typeof setTimeout> | undefined;
  /** El servidor ha cerrado el seguimiento: se termina en cuanto la lista llega al final. */
  private serverDone = false;
  private lastSubmission: DocumentSubmission | null = null;
  /** Peticiones en curso (envío o carga y SSE). Se cancelan todas juntas. */
  private active = new Subscription();

  readonly phase = this._phase.asReadonly();
  readonly status = this._status.asReadonly();
  readonly documentId = this._documentId.asReadonly();
  readonly error = this._error.asReadonly();
  readonly steps = computed(() => {
    const shown = this._shown();
    return shown === null ? null : stepsAt(shown);
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  submit(submission: DocumentSubmission): void {
    this.stop();
    // El token anti-bots es de un solo uso: un reintento no lo reenvía (el formulario pide uno nuevo).
    this.lastSubmission = { ...submission, humanToken: undefined };
    this._phase.set('submitting');
    this.clearProgress();
    this._documentId.set(null);
    this._error.set(null);

    this.active.add(
      this.api.submit(submission).subscribe({
        next: (response) => this.track(response.id, response.status),
        error: (error: unknown) => this.fail(messageOf(error)),
      }),
    );
  }

  /**
   * Si el servidor ya aceptó el documento y solo se perdió el seguimiento, vuelve a seguirlo sin reenviarlo (cada envío
   * crea un documento y una adaptación nuevos). Si el envío falló o la adaptación terminó con error, lo reenvía.
   */
  retry(): void {
    const id = this._documentId();
    const status = this._status();
    if (id !== null && status !== null && status !== 'Error') {
      this.stop();
      this._error.set(null);
      this.track(id, status, false);
      return;
    }
    if (this.lastSubmission !== null) {
      this.submit(this.lastSubmission);
    }
  }

  reset(): void {
    this.stop();
    this.lastSubmission = null;
    this._phase.set('idle');
    this.clearProgress();
    this._documentId.set(null);
    this._error.set(null);
  }

  /** @param fresh Si es un documento nuevo: la lista empieza en su estado. Al volver a seguirlo, sigue donde estaba. */
  private track(id: string, status: DocumentStatus, fresh = true): void {
    this._documentId.set(id);
    this._phase.set('tracking');
    this.serverDone = false;
    // Al volver a seguirlo, la lista sigue donde estaba: el primer evento trae el estado actual.
    if (fresh || this._shown() === null) {
      this.show(progressPosition(status));
      this.moveTo({ status, stage: null });
    }
    this.active.add(
      this.events.watchProgress(id).subscribe({
        next: (progress) => this.moveTo(progress),
        complete: () => {
          this.serverDone = true;
          this.advance();
        },
        error: (error: unknown) => this.fail(messageOf(error)),
      }),
    );
  }

  private moveTo(progress: DocumentProgress): void {
    this._status.set(progress.status);
    // Al fallar, el servidor ya no dice la fase: se marca con error la última que dijo (como pronto, la de adaptar:
    // si falla, el documento ya se había recibido).
    const target = progressPosition(progress.status, progress.stage, progress.part ?? null);
    const last = this.target ?? this._shown();
    this.target =
      target.failed && progress.stage === null && last !== null && last.index > target.index
        ? { ...last, retrying: false, failed: true }
        : target;
    this.advance();
  }

  /** Enseña el siguiente paso camino de `target` si el actual ya lleva bastante a la vista, o espera. */
  private advance(): void {
    clearTimeout(this.stepTimer);
    for (;;) {
      const shown = this._shown();
      const next = shown === null || this.target === null ? null : nextPosition(shown, this.target);
      if (next === null) {
        this.settle();
        return;
      }
      const wait = this.shownSince + this.stepMs - Date.now();
      if (wait > 0 && !next.failed) {
        this.stepTimer = setTimeout(() => this.advance(), wait);
        return;
      }
      this.show(next);
    }
  }

  private settle(): void {
    if (!this.serverDone || this._phase() !== 'tracking') {
      return;
    }
    const status = this._status();
    if (status === 'Error') {
      this.fail(PROGRESS_TEXTS.adaptationFailed);
      return;
    }
    // Si el servidor deja de enviar eventos antes de terminar (tiene un tiempo máximo), se abre el documento igual:
    // su página sigue el progreso.
    const shown = this._shown();
    if ((status !== null && isAwaitingAdaptation(status)) || (shown !== null && isReady(shown))) {
      this._phase.set('ready');
    }
  }

  private show(position: ProgressPosition): void {
    this._shown.set(position);
    this.shownSince = Date.now();
  }

  private clearProgress(): void {
    this._status.set(null);
    this._shown.set(null);
    this.target = null;
    this.serverDone = false;
  }

  private fail(message: string): void {
    this._error.set(message);
    this._phase.set('failed');
  }

  private stop(): void {
    clearTimeout(this.stepTimer);
    this.active.unsubscribe();
    this.active = new Subscription();
  }
}

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : ERROR_TEXTS.generic;
}
