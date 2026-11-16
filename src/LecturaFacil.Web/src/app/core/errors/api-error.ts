// SPDX-License-Identifier: Apache-2.0
import { ERROR_TEXTS } from '../ui-texts';

/**
 * Error de la API ya traducido para el usuario. Solo lleva el mensaje en español del catálogo, el código HTTP y el
 * código del error (si es uno conocido): nunca el detalle técnico que pueda venir en el ProblemDetails.
 */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code: string | null,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/** Códigos de error de la API (auth, DocumentErrors, ReviewErrors y ExportErrors) con su mensaje para el usuario. */
const MESSAGES_BY_CODE: Readonly<Record<string, string>> = {
  'auth.invalid_credentials': ERROR_TEXTS.invalidCredentials,
  'auth.antiforgery': ERROR_TEXTS.pageExpired,
  'human_verification.failed': ERROR_TEXTS.humanVerification,
  'document.not_found': ERROR_TEXTS.notFound,
  'document.file_too_large': ERROR_TEXTS.fileTooLarge,
  'document.invalid_file_type': ERROR_TEXTS.invalidFileType,
  'document.scanned_pdf': ERROR_TEXTS.scannedPdf,
  'document.unreadable_pdf': ERROR_TEXTS.unreadablePdf,
  'document.pdf_too_complex': ERROR_TEXTS.pdfTooComplex,
  'document.busy': ERROR_TEXTS.unavailable,
  'document.text_too_short': ERROR_TEXTS.textTooShort,
  'document.text_too_long': ERROR_TEXTS.textTooLong,
  'review.version_conflict': ERROR_TEXTS.conflict,
  'review.not_editable': ERROR_TEXTS.notEditable,
  'review.incomplete_edit': ERROR_TEXTS.incompleteEdit,
  'review.edit_too_long': ERROR_TEXTS.editTooLong,
  'review.key_fact_not_found': ERROR_TEXTS.keyFactNotFound,
  'export.pending_warnings': ERROR_TEXTS.exportPendingWarnings,
  'export.unsupported_format': ERROR_TEXTS.exportUnsupportedFormat,
  'export.nothing_to_export': ERROR_TEXTS.exportNothing,
};

const MESSAGES_BY_STATUS: Readonly<Record<number, string>> = {
  0: ERROR_TEXTS.offline,
  400: ERROR_TEXTS.invalidRequest,
  401: ERROR_TEXTS.sessionExpired,
  404: ERROR_TEXTS.notFound,
  409: ERROR_TEXTS.conflict,
  413: ERROR_TEXTS.fileTooLarge,
  415: ERROR_TEXTS.unsupported,
  422: ERROR_TEXTS.unprocessable,
  429: ERROR_TEXTS.tooManyRequests,
  503: ERROR_TEXTS.unavailable,
};

/** Construye el error para el usuario a partir del código HTTP y del cuerpo (ProblemDetails o cualquier otra cosa). */
export function toApiError(status: number, body: unknown): ApiError {
  const code = readCode(body);
  const knownCode = code !== null && code in MESSAGES_BY_CODE ? code : null;
  const message = knownCode
    ? MESSAGES_BY_CODE[knownCode]
    : (MESSAGES_BY_STATUS[status] ?? ERROR_TEXTS.generic);
  return new ApiError(message, status, knownCode);
}

function readCode(body: unknown): string | null {
  if (typeof body !== 'object' || body === null || !('code' in body)) {
    return null;
  }
  const code = (body as { code: unknown }).code;
  return typeof code === 'string' ? code : null;
}
