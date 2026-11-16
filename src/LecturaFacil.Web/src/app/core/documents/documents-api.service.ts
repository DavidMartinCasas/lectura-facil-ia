// SPDX-License-Identifier: Apache-2.0
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  DocumentDetails,
  DocumentListItem,
  DocumentSubmission,
  ExportFormat,
  ManualEdit,
  SubmitDocumentResponse,
} from './document.models';

export const DOCUMENTS_PATH = '/api/documents';
/** Cabecera con el token del desafío anti-bots (US-16). Sirve igual para el JSON que para el PDF. */
export const HUMAN_TOKEN_HEADER = 'X-Turnstile-Token';

/** Llamadas HTTP a /api/documents. Los errores llegan ya traducidos por el interceptor (ApiError). */
@Injectable({ providedIn: 'root' })
export class DocumentsApiService {
  private readonly http = inject(HttpClient);

  submit(submission: DocumentSubmission): Observable<SubmitDocumentResponse> {
    const headers = submission.humanToken
      ? new HttpHeaders({ [HUMAN_TOKEN_HEADER]: submission.humanToken })
      : undefined;
    if (submission.kind === 'pdf') {
      const form = new FormData();
      form.append('file', submission.file);
      return this.http.post<SubmitDocumentResponse>(DOCUMENTS_PATH, form, { headers });
    }

    const title = submission.title?.trim();
    return this.http.post<SubmitDocumentResponse>(
      DOCUMENTS_PATH,
      { text: submission.text, title: title ? title : null },
      { headers },
    );
  }

  list(): Observable<readonly DocumentListItem[]> {
    return this.http.get<readonly DocumentListItem[]>(DOCUMENTS_PATH);
  }

  get(id: string): Observable<DocumentDetails> {
    return this.http.get<DocumentDetails>(documentPath(id));
  }

  /** Guarda la edición de la versión `version` (la última) como una versión nueva. Devuelve el documento actualizado. */
  saveEdit(id: string, version: number, edit: ManualEdit): Observable<DocumentDetails> {
    return this.http.put<DocumentDetails>(`${documentPath(id)}/versions/${version}`, edit);
  }

  /** Marca como revisado el aviso del dato clave `factId` de la versión `version`. */
  acknowledgeKeyFact(id: string, version: number, factId: number): Observable<DocumentDetails> {
    return this.http.post<DocumentDetails>(`${documentPath(id)}/key-facts/${factId}/acknowledge`, {
      version,
    });
  }

  /** Exporta la última versión en el formato elegido. El servidor genera el fichero; aquí solo se descarga. */
  exportDocument(id: string, format: ExportFormat): Observable<DownloadedFile> {
    return this.http
      .post(`${documentPath(id)}/export`, null, {
        params: { format },
        responseType: 'blob',
        observe: 'response',
      })
      .pipe(map((response) => toDownloadedFile(response, format)));
  }
}

function toDownloadedFile(response: HttpResponse<Blob>, format: ExportFormat): DownloadedFile {
  return {
    blob: response.body ?? new Blob(),
    fileName: fileNameFrom(response.headers.get('Content-Disposition')) ?? `documento.${format}`,
  };
}

/**
 * Nombre del fichero de la cabecera Content-Disposition. El servidor ya lo sanea; aquí se vuelve a filtrar por si acaso
 * (solo letras, cifras, guiones, puntos y guiones bajos), porque el navegador lo usa para guardar el fichero.
 */
export function fileNameFrom(disposition: string | null): string | null {
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  const name = match?.[1]?.trim() ?? '';
  return /^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$/.test(name) ? name : null;
}

/** Fichero descargado de la API, con el nombre que propone el servidor. */
export interface DownloadedFile {
  readonly blob: Blob;
  readonly fileName: string;
}

function documentPath(id: string): string {
  return `${DOCUMENTS_PATH}/${encodeURIComponent(id)}`;
}
