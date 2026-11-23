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
  ExportFormat,
  ManualEdit,
  ValidationResult,
  isAwaitingAdaptation,
} from './document.models';
import { DocumentsApiService } from './documents-api.service';
import { FileDownloadService } from './file-download.service';

/** Acciones que terminan bien y se anuncian a los lectores de pantalla. */
export type DocumentAction =
  | 'saved'
  | 'acknowledged'
  | 'acknowledgedAll'
  | 'exported'
  | 'retried'
  | 'sent'
  | 'approved'
  | 'changesRequested'
  | 'withdrawn'
  | 'published';

/**
 * Estado de la página de un documento: lo carga y, si aún se está adaptando, sigue su progreso y lo vuelve a cargar
 * al terminar. También guarda las ediciones y marca los datos clave revisados; el servidor devuelve el documento ya
 * recalculado (pautas, datos clave y legibilidad), así que aquí no se calcula nada. También descarga la exportación que
 * genera el servidor (US-14). Se provee en el componente de la página.
 */
@Injectable()
export class DocumentViewStore {
  private readonly api = inject(DocumentsApiService);
  private readonly events = inject(DocumentEventsService);
  private readonly downloads = inject(FileDownloadService);

  private readonly _document = signal<DocumentDetails | null>(null);
  private readonly _status = signal<DocumentStatus | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _saving = signal(false);
  private readonly _actionError = signal<string | null>(null);
  private readonly _exporting = signal(false);
  private readonly _lastAction = signal<DocumentAction | null>(null);
  /** Peticiones en curso (envío o carga y SSE). Se cancelan todas juntas. */
  private active = new Subscription();

  readonly document = this._document.asReadonly();
  readonly status = this._status.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  /** Hay una edición o una marca de revisado en curso. */
  readonly saving = this._saving.asReadonly();
  /** Hay una exportación en curso. */
  readonly exporting = this._exporting.asReadonly();
  /** Mensaje para el usuario si la última edición, marca o exportación ha fallado. */
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

  /** Marca como revisados todos los avisos de los datos clave de la versión en pantalla, de una vez. */
  acknowledgeAllKeyFacts(): void {
    const document = this._document();
    if (!document?.adaptation || this._saving()) {
      return;
    }
    this.run(
      this.api.acknowledgeAllKeyFacts(document.id, document.adaptation.version),
      'acknowledgedAll',
    );
  }

  /** Envía a validación la versión en pantalla, si sus avisos de datos clave ya están revisados. */
  sendToValidation(): void {
    const document = this._document();
    if (!document?.adaptation || !document.canSendToValidation || this._saving()) {
      return;
    }
    this.run(this.api.sendToValidation(document.id, document.adaptation.version), 'sent');
  }

  /** Anota lo que decidió el grupo de validación. */
  recordValidation(result: ValidationResult): void {
    const document = this._document();
    if (document?.status !== 'PendingValidation' || this._saving()) {
      return;
    }
    this.run(
      this.api.recordValidation(document.id, result),
      result.outcome === 'Approved' ? 'approved' : 'changesRequested',
    );
  }

  /** Retira el documento de la validación: vuelve a ser un borrador. */
  withdrawFromValidation(): void {
    const document = this._document();
    if (document?.status !== 'PendingValidation' || this._saving()) {
      return;
    }
    this.run(this.api.withdrawFromValidation(document.id), 'withdrawn');
  }

  /** Publica un documento validado. */
  publish(): void {
    const document = this._document();
    if (document?.status !== 'Validated' || this._saving()) {
      return;
    }
    this.run(this.api.publish(document.id), 'published');
  }

  /**
   * Vuelve a adaptar un documento cuya adaptación ha fallado. El servidor lo deja en "Procesando" y aquí se sigue su
   * progreso como al enviarlo.
   */
  retryAdaptation(): void {
    const document = this._document();
    if (!document?.canRetry || this._saving()) {
      return;
    }
    this._saving.set(true);
    this._actionError.set(null);
    this._lastAction.set(null);
    this.active.add(
      this.api.retryAdaptation(document.id).subscribe({
        next: (updated) => {
          this._saving.set(false);
          this._lastAction.set('retried');
          this.show(updated);
        },
        error: (error: unknown) => {
          this._saving.set(false);
          this._actionError.set(error instanceof ApiError ? error.message : ERROR_TEXTS.generic);
        },
      }),
    );
  }

  /** Descarga la última versión en el formato elegido. Solo se ofrece cuando la adaptación se puede exportar. */
  exportAdaptation(format: ExportFormat): void {
    const document = this._document();
    // Mientras se guarda una edición, se exportaría la versión anterior: se espera a que termine.
    if (!document?.adaptation?.canExport || this._exporting() || this._saving()) {
      return;
    }
    this._exporting.set(true);
    this._actionError.set(null);
    this._lastAction.set(null);
    this.active.add(
      this.api.exportDocument(document.id, format).subscribe({
        next: (file) => {
          this._exporting.set(false);
          this.downloads.save(file.blob, file.fileName);
          this._lastAction.set('exported');
        },
        error: (error: unknown) => {
          this._exporting.set(false);
          this._actionError.set(error instanceof ApiError ? error.message : ERROR_TEXTS.generic);
        },
      }),
    );
  }

  private run(request: Observable<DocumentDetails>, action: DocumentAction): void {
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
    this._exporting.set(false);
  }

  private fail(error: unknown): void {
    this._loading.set(false);
    this._error.set(error instanceof ApiError ? error.message : ERROR_TEXTS.generic);
  }
}
