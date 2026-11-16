// SPDX-License-Identifier: Apache-2.0
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import { progressSteps } from './document-progress';
import { DocumentDetails, DocumentStatus, isAwaitingAdaptation } from './document.models';
import { DocumentsApiService } from './documents-api.service';

/**
 * Estado de la página de un documento: lo carga y, si aún se está adaptando, sigue su progreso y lo vuelve a cargar
 * al terminar. Se provee en el componente de la página.
 */
@Injectable()
export class DocumentViewStore {
  private readonly api = inject(DocumentsApiService);
  private readonly events = inject(DocumentEventsService);

  private readonly _document = signal<DocumentDetails | null>(null);
  private readonly _status = signal<DocumentStatus | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  /** Peticiones en curso (envío o carga y SSE). Se cancelan todas juntas. */
  private active = new Subscription();

  readonly document = this._document.asReadonly();
  readonly status = this._status.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly steps = computed(() => {
    const status = this._status();
    return status === null ? null : progressSteps(status);
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  load(id: string): void {
    this.stop();
    this._loading.set(true);
    this._error.set(null);
    this.active.add(
      this.api.get(id).subscribe({
        next: (document) => this.show(document),
        error: (error: unknown) => this.fail(error),
      }),
    );
  }

  private show(document: DocumentDetails): void {
    this._document.set(document);
    this._status.set(document.status);
    this._loading.set(false);
    if (isAwaitingAdaptation(document.status)) {
      this.active.add(
        this.events.watch(document.id).subscribe({
          next: (status) => this._status.set(status),
          complete: () => this.load(document.id),
          error: (error: unknown) => this.fail(error),
        }),
      );
    }
  }

  private stop(): void {
    this.active.unsubscribe();
    this.active = new Subscription();
  }

  private fail(error: unknown): void {
    this._loading.set(false);
    this._error.set(error instanceof ApiError ? error.message : ERROR_TEXTS.generic);
  }
}
