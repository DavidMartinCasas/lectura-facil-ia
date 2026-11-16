// SPDX-License-Identifier: Apache-2.0
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DocumentDetails,
  DocumentListItem,
  DocumentSubmission,
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
    return this.http.get<DocumentDetails>(`${DOCUMENTS_PATH}/${encodeURIComponent(id)}`);
  }
}
