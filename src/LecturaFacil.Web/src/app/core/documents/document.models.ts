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

/** GET /api/documents/{id}. Todo el texto es texto plano: nunca se interpreta como HTML. */
export interface DocumentDetails {
  readonly id: string;
  readonly title: string;
  readonly status: DocumentStatus;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly error: { readonly code: string; readonly message: string } | null;
  readonly adaptation: Adaptation | null;
  readonly original: OriginalText;
  readonly readability: ReadabilityComparison | null;
  /** Un borrador ya adaptado: se puede editar y marcar sus avisos como revisados. Lo decide el servidor. */
  readonly editable: boolean;
}

/** Documento con una adaptación que revisar. */
export type ReviewableDocument = DocumentDetails & { readonly adaptation: Adaptation };

export function isReviewable(document: DocumentDetails): document is ReviewableDocument {
  return document.adaptation !== null;
}

/** Texto original y sus problemas. Las posiciones son índices UTF-16 sobre `text`, con el fin excluido. */
export interface OriginalText {
  readonly text: string;
  readonly findings: readonly OriginalFinding[];
}

export interface OriginalFinding {
  readonly guidelineCode: string;
  /** Descripción de la pauta que incumple, por ejemplo "Frases cortas". */
  readonly guideline: string;
  readonly fragment: string;
  readonly start: number;
  readonly end: number;
  readonly severity: Severity;
  readonly suggestion: string;
}

export type Severity = 'High' | 'Medium' | 'Low';

/** Última versión adaptada. Siempre es un borrador pendiente de validación humana. */
export interface Adaptation {
  readonly version: number;
  readonly title: string;
  readonly sections: readonly AdaptedSection[];
  readonly glossary: readonly GlossaryTerm[];
  readonly pendingHumanValidation: boolean;
  readonly promptVersion: string;
  readonly createdAt: string;
  readonly guidelines: GuidelineReport | null;
  readonly verification: MeaningVerification | null;
  /** Nombre visible del proveedor que la procesó, por ejemplo "Servidor propio" o "Azure (UE)". */
  readonly provider: string;
  readonly pendingKeyFactCount: number;
  /** `false` mientras quede algún aviso de datos clave sin marcar como revisado. */
  readonly canExport: boolean;
}

export interface AdaptedSection {
  readonly question: string;
  readonly sentences: readonly string[];
}

export interface GlossaryTerm {
  readonly term: string;
  readonly definition: string;
}

export type GuidelineStatus = 'Compliant' | 'NotCompliant' | 'NotChecked';

export interface GuidelineReport {
  readonly summary: string;
  readonly compliantCount: number;
  readonly totalCount: number;
  readonly items: readonly GuidelineResult[];
}

export interface GuidelineResult {
  readonly code: string;
  readonly description: string;
  readonly severity: Severity;
  readonly status: GuidelineStatus;
  readonly findingCount: number;
  readonly findings: readonly GuidelineFinding[];
}

export interface GuidelineFinding {
  readonly fragment: string;
  readonly start: number;
  readonly end: number;
  readonly severity: Severity;
  readonly suggestion: string;
}

export type KeyFactStatus = 'Conserved' | 'Review' | 'Omitted';

export type KeyFactType =
  | 'Deadline'
  | 'Amount'
  | 'Requirement'
  | 'Date'
  | 'Place'
  | 'Document'
  | 'Reference'
  | 'Figure'
  | 'Other';

export interface MeaningVerification {
  readonly status: 'Verified' | 'NotVerified';
  readonly summary: string;
  readonly conservedCount: number;
  readonly reviewCount: number;
  readonly omittedCount: number;
  readonly keyFacts: readonly KeyFact[];
}

export interface KeyFact {
  /** Posición en la lista de su versión: el `factId` para marcarlo como revisado. */
  readonly id: number;
  readonly type: KeyFactType;
  readonly originalValue: string;
  readonly adaptedValue: string | null;
  readonly status: KeyFactStatus;
  readonly reason: string;
  readonly reasonMessage: string;
  readonly acknowledged: boolean;
  readonly requiresAcknowledgement: boolean;
}

export interface ReadabilityScore {
  readonly value: number;
  readonly grade: string;
  readonly gradeLabel: string;
}

export interface ReadabilityComparison {
  readonly original: ReadabilityScore;
  readonly adapted: ReadabilityScore;
  readonly improvement: number;
}

/** Cuerpo de PUT /api/documents/{id}/versions/{n}: la adaptación completa editada. */
export interface ManualEdit {
  readonly title: string;
  readonly sections: readonly AdaptedSection[];
  readonly glossary: readonly GlossaryTerm[];
}

/** Evento SSE "status" de GET /api/documents/{id}/events. */
export interface DocumentStatusEvent {
  readonly id: string;
  readonly status: DocumentStatus;
}
