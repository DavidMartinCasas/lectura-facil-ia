// SPDX-License-Identifier: Apache-2.0
// Tipos escritos a mano que coinciden con el OpenAPI de la API (LecturaFacil.Api/Documents).

export const DOCUMENT_STATUSES = [
  'Received',
  'Processing',
  'Error',
  'Draft',
  'PendingValidation',
  'Validated',
  'Published',
] as const;

export type DocumentStatus = (typeof DOCUMENT_STATUSES)[number];

export function isDocumentStatus(value: unknown): value is DocumentStatus {
  return typeof value === 'string' && (DOCUMENT_STATUSES as readonly string[]).includes(value);
}

/** El documento sigue en la cola o adaptándose: hay que seguir su progreso. */
export function isAwaitingAdaptation(status: DocumentStatus): boolean {
  return status === 'Received' || status === 'Processing';
}

/** Lo que el usuario envía: un texto pegado o un PDF. */
export type DocumentSubmission =
  | { readonly kind: 'text'; readonly text: string; readonly title?: string }
  | { readonly kind: 'pdf'; readonly file: File };

/** Respuesta de POST /api/documents. */
export interface SubmitDocumentResponse {
  readonly id: string;
  readonly status: DocumentStatus;
}

/** Elemento de GET /api/documents. */
export interface DocumentListItem {
  readonly id: string;
  readonly title: string;
  readonly status: DocumentStatus;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly provider: string | null;
}

/** GET /api/documents/{id}. La adaptación completa se usará en la US-13; aquí solo hace falta saber si existe. */
export interface DocumentDetails {
  readonly id: string;
  readonly title: string;
  readonly status: DocumentStatus;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly error: { readonly code: string; readonly message: string } | null;
  readonly adaptation: {
    readonly version: number;
    readonly pendingHumanValidation: boolean;
  } | null;
}

/** Evento SSE "status" de GET /api/documents/{id}/events. */
export interface DocumentStatusEvent {
  readonly id: string;
  readonly status: DocumentStatus;
}
