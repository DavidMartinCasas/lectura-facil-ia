// SPDX-License-Identifier: Apache-2.0
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable, distinctUntilChanged, map } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
import { DocumentProgress, PartProgress, isProcessingStage } from './document-progress';
import { DOCUMENTS_PATH } from './documents-api.service';
import { DocumentStatus, isAwaitingAdaptation, isDocumentStatus } from './document.models';

/** Lo que el servicio usa de un EventSource. Permite sustituirlo por un doble en los tests. */
export interface EventSourceLike {
  readonly readyState: number;
  onerror: ((event: Event) => void) | null;
  addEventListener(type: 'status', listener: (event: MessageEvent<string>) => void): void;
  close(): void;
}

export const EVENT_SOURCE_FACTORY = new InjectionToken<(url: string) => EventSourceLike>(
  'EVENT_SOURCE_FACTORY',
  { providedIn: 'root', factory: () => (url: string) => new EventSource(url) },
);

/** Opciones de reconexión. Se pueden cambiar en los tests. */
export const DOCUMENT_EVENTS_RETRY = new InjectionToken<{ maxRetries: number; delayMs: number }>(
  'DOCUMENT_EVENTS_RETRY',
  { providedIn: 'root', factory: () => ({ maxRetries: 5, delayMs: 2_000 }) },
);

const STATUS_EVENT = 'status';
const CLOSED = 2;

/**
 * Sigue el estado de un documento por Server-Sent Events. Emite cada estado (y, mientras se adapta, cada fase) y termina cuando el documento deja de
 * estar en la cola o adaptándose. Si se corta la conexión, vuelve a conectarse: el navegador lo hace solo y, si
 * el EventSource se cierra del todo, lo reabre el servicio. Tras demasiados fallos seguidos, emite un ApiError.
 */
@Injectable({ providedIn: 'root' })
export class DocumentEventsService {
  private readonly createEventSource = inject(EVENT_SOURCE_FACTORY);
  private readonly retry = inject(DOCUMENT_EVENTS_RETRY);

  /** Solo los cambios de estado, sin las fases. */
  watch(id: string): Observable<DocumentStatus> {
    return this.watchProgress(id).pipe(
      map((progress) => progress.status),
      distinctUntilChanged(),
    );
  }

  /** Cada cambio de estado o de fase. */
  watchProgress(id: string): Observable<DocumentProgress> {
    const url = `${DOCUMENTS_PATH}/${encodeURIComponent(id)}/events`;

    return new Observable<DocumentProgress>((subscriber) => {
      let source: EventSourceLike | null = null;
      let failures = 0;
      let reconnectTimer: ReturnType<typeof setTimeout> | undefined;

      const open = (): void => {
        source = this.createEventSource(url);
        source.addEventListener(STATUS_EVENT, (event: MessageEvent<string>) => {
          const progress = parseProgress(event.data);
          if (progress === null) {
            return;
          }
          failures = 0;
          subscriber.next(progress);
          if (!isAwaitingAdaptation(progress.status)) {
            source?.close();
            subscriber.complete();
          }
        });
        source.onerror = () => {
          failures++;
          if (failures > this.retry.maxRetries) {
            source?.close();
            subscriber.error(new ApiError(ERROR_TEXTS.progressLost, 0, null));
            return;
          }
          if (source?.readyState === CLOSED) {
            reconnectTimer = setTimeout(open, this.retry.delayMs);
          }
        };
      };

      open();

      return () => {
        clearTimeout(reconnectTimer);
        source?.close();
      };
    });
  }
}

/** La parte de un documento largo, si el evento la trae bien: dos enteros con 1 ≤ parte ≤ total. */
function partOf(part: unknown, parts: unknown): PartProgress | null {
  return Number.isInteger(part) &&
    Number.isInteger(parts) &&
    (part as number) >= 1 &&
    (part as number) <= (parts as number)
    ? { number: part as number, count: parts as number }
    : null;
}

function parseProgress(data: string): DocumentProgress | null {
  try {
    const parsed: unknown = JSON.parse(data);
    if (typeof parsed === 'object' && parsed !== null && 'status' in parsed) {
      const { status, stage, part, parts } = parsed as {
        status: unknown;
        stage?: unknown;
        part?: unknown;
        parts?: unknown;
      };
      if (!isDocumentStatus(status)) {
        return null;
      }
      // Una fase o una parte desconocidas no rompen el seguimiento: se sigue solo con el estado.
      const progress: DocumentProgress = { status, stage: isProcessingStage(stage) ? stage : null };
      const known = partOf(part, parts);
      return known === null ? progress : { ...progress, part: known };
    }
  } catch {
    // Un evento mal formado se ignora: el siguiente traerá el estado.
  }
  return null;
}
