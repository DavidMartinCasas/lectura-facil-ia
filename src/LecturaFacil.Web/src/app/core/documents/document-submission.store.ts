// SPDX-License-Identifier: Apache-2.0
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS, PROGRESS_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import { progressSteps } from './document-progress';
import { DocumentStatus, DocumentSubmission } from './document.models';
import { DocumentsApiService } from './documents-api.service';

export type SubmissionPhase = 'idle' | 'submitting' | 'tracking' | 'ready' | 'failed';

/**
 * Estado del envío de un documento en la pantalla "Nuevo documento": envía el texto o el PDF, sigue su progreso por
 * SSE y guarda el último envío para poder reintentarlo. Se provee en el componente de la página.
 */
@Injectable()
export class DocumentSubmissionStore {
  private readonly api = inject(DocumentsApiService);
  private readonly events = inject(DocumentEventsService);

  private readonly _phase = signal<SubmissionPhase>('idle');
  private readonly _status = signal<DocumentStatus | null>(null);
  private readonly _documentId = signal<string | null>(null);
  private readonly _error = signal<string | null>(null);
  private lastSubmission: DocumentSubmission | null = null;
  /** Peticiones en curso (envío o carga y SSE). Se cancelan todas juntas. */
  private active = new Subscription();

  readonly phase = this._phase.asReadonly();
  readonly status = this._status.asReadonly();
  readonly documentId = this._documentId.asReadonly();
  readonly error = this._error.asReadonly();
  readonly steps = computed(() => {
    const status = this._status();
    return status === null ? null : progressSteps(status);
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  submit(submission: DocumentSubmission): void {
    this.stop();
    this.lastSubmission = submission;
    this._phase.set('submitting');
    this._status.set(null);
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
      this.track(id, status);
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
    this._status.set(null);
    this._documentId.set(null);
    this._error.set(null);
  }

  private track(id: string, status: DocumentStatus): void {
    this._documentId.set(id);
    this._status.set(status);
    this._phase.set('tracking');
    this.active.add(
      this.events.watch(id).subscribe({
        next: (next) => this._status.set(next),
        complete: () => this.finish(),
        error: (error: unknown) => this.fail(messageOf(error)),
      }),
    );
  }

  private finish(): void {
    if (this._status() === 'Error') {
      this.fail(PROGRESS_TEXTS.adaptationFailed);
      return;
    }
    this._phase.set('ready');
  }

  private fail(message: string): void {
    this._error.set(message);
    this._phase.set('failed');
  }

  private stop(): void {
    this.active.unsubscribe();
    this.active = new Subscription();
  }
}

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : ERROR_TEXTS.generic;
}
