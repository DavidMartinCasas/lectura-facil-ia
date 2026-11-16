// SPDX-License-Identifier: Apache-2.0
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
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
 * Sigue el estado de un documento por Server-Sent Events. Emite cada estado y termina cuando el documento deja de
 * estar en la cola o adaptándose. Si se corta la conexión, vuelve a conectarse: el navegador lo hace solo y, si
 * el EventSource se cierra del todo, lo reabre el servicio. Tras demasiados fallos seguidos, emite un ApiError.
 */
@Injectable({ providedIn: 'root' })
export class DocumentEventsService {
  private readonly createEventSource = inject(EVENT_SOURCE_FACTORY);
  private readonly retry = inject(DOCUMENT_EVENTS_RETRY);

  watch(id: string): Observable<DocumentStatus> {
    const url = `${DOCUMENTS_PATH}/${encodeURIComponent(id)}/events`;

    return new Observable<DocumentStatus>((subscriber) => {
      let source: EventSourceLike | null = null;
      let failures = 0;
      let reconnectTimer: ReturnType<typeof setTimeout> | undefined;

      const open = (): void => {
        source = this.createEventSource(url);
        source.addEventListener(STATUS_EVENT, (event: MessageEvent<string>) => {
          const status = parseStatus(event.data);
          if (status === null) {
            return;
          }
          failures = 0;
          subscriber.next(status);
          if (!isAwaitingAdaptation(status)) {
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

function parseStatus(data: string): DocumentStatus | null {
  try {
    const parsed: unknown = JSON.parse(data);
    if (typeof parsed === 'object' && parsed !== null && 'status' in parsed) {
      const status = (parsed as { status: unknown }).status;
      return isDocumentStatus(status) ? status : null;
    }
  } catch {
    // Un evento mal formado se ignora: el siguiente traerá el estado.
  }
  return null;
}
