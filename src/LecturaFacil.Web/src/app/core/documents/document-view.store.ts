// SPDX-License-Identifier: Apache-2.0
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import { progressSteps } from './document-progress';
import {
  DocumentDetails,
  DocumentStatus,
  ManualEdit,
  isAwaitingAdaptation,
} from './document.models';
import { DocumentsApiService } from './documents-api.service';

/**
 * Estado de la página de un documento: lo carga y, si aún se está adaptando, sigue su progreso y lo vuelve a cargar
 * al terminar. También guarda las ediciones y marca los datos clave revisados; el servidor devuelve el documento ya
 * recalculado (pautas, datos clave y legibilidad), así que aquí no se calcula nada. Se provee en el componente de la página.
 */
@Injectable()
export class DocumentViewStore {
  private readonly api = inject(DocumentsApiService);
  private readonly events = inject(DocumentEventsService);

  private readonly _document = signal<DocumentDetails | null>(null);
  private readonly _status = signal<DocumentStatus | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _saving = signal(false);
  private readonly _actionError = signal<string | null>(null);
  private readonly _lastAction = signal<'saved' | 'acknowledged' | null>(null);
  /** Peticiones en curso (envío o carga y SSE). Se cancelan todas juntas. */
  private active = new Subscription();

  readonly document = this._document.asReadonly();
  readonly status = this._status.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  /** Hay una edición o una marca de revisado en curso. */
  readonly saving = this._saving.asReadonly();
  /** Mensaje para el usuario si la última edición o marca ha fallado. */
  readonly actionError = this._actionError.asReadonly();
  /** Última acción que ha terminado bien, para anunciarla a los lectores de pantalla. */
  readonly lastAction = this._lastAction.asReadonly();
  readonly steps = computed(() => {
    const status = this._status();
    return status === null ? null : progressSteps(status);
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  load(id: string): void {
    this.stop();
    if (this._document()?.id !== id) {
      this._document.set(null);
      this._status.set(null);
    }
    this._loading.set(true);
    this._error.set(null);
    this._actionError.set(null);
    this.active.add(
      this.api.get(id).subscribe({
        next: (document) => this.show(document),
        error: (error: unknown) => this.fail(error),
      }),
    );
  }

  saveEdit(edit: ManualEdit): void {
    const document = this._document();
    if (!document?.adaptation || this._saving()) {
      return;
    }
    this.run(this.api.saveEdit(document.id, document.adaptation.version, edit), 'saved');
  }

  acknowledgeKeyFact(factId: number): void {
    const document = this._document();
    if (!document?.adaptation || this._saving()) {
      return;
    }
    this.run(
      this.api.acknowledgeKeyFact(document.id, document.adaptation.version, factId),
      'acknowledged',
    );
  }

  private run(request: Observable<DocumentDetails>, action: 'saved' | 'acknowledged'): void {
    this._saving.set(true);
    this._actionError.set(null);
    this._lastAction.set(null);
    this.active.add(
      request.subscribe({
        next: (updated) => {
          this._saving.set(false);
          this._document.set(updated);
          this._status.set(updated.status);
          this._lastAction.set(action);
        },
        error: (error: unknown) => {
          this._saving.set(false);
          this._actionError.set(error instanceof ApiError ? error.message : ERROR_TEXTS.generic);
        },
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
    this._saving.set(false);
  }

  private fail(error: unknown): void {
    this._loading.set(false);
    this._error.set(error instanceof ApiError ? error.message : ERROR_TEXTS.generic);
  }
}
