// SPDX-License-Identifier: Apache-2.0
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export const SAMPLES_PATH = '/api/samples';

/** Texto de ejemplo de GET /api/samples (US-16). */
export interface SampleText {
  readonly id: string;
  readonly title: string;
  readonly source: string;
  readonly text: string;
}

@Injectable({ providedIn: 'root' })
export class SamplesApiService {
  private readonly http = inject(HttpClient);

  list(): Observable<readonly SampleText[]> {
    return this.http.get<readonly SampleText[]>(SAMPLES_PATH);
  }
}
