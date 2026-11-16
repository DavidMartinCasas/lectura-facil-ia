// SPDX-License-Identifier: Apache-2.0
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DocumentDetails,
  DocumentListItem,
  DocumentSubmission,
  ManualEdit,
  SubmitDocumentResponse,
} from './document.models';

export const DOCUMENTS_PATH = '/api/documents';

/** Llamadas HTTP a /api/documents. Los errores llegan ya traducidos por el interceptor (ApiError). */
@Injectable({ providedIn: 'root' })
export class DocumentsApiService {
  private readonly http = inject(HttpClient);

  submit(submission: DocumentSubmission): Observable<SubmitDocumentResponse> {
    if (submission.kind === 'pdf') {
      const form = new FormData();
      form.append('file', submission.file);
      return this.http.post<SubmitDocumentResponse>(DOCUMENTS_PATH, form);
    }

    const title = submission.title?.trim();
    return this.http.post<SubmitDocumentResponse>(DOCUMENTS_PATH, {
      text: submission.text,
      title: title ? title : null,
    });
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
}

function documentPath(id: string): string {
  return `${DOCUMENTS_PATH}/${encodeURIComponent(id)}`;
}
