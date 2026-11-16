// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DocumentListItem } from '../../core/documents/document.models';
import { DocumentsApiService } from '../../core/documents/documents-api.service';
import { ApiError } from '../../core/errors/api-error';
import { ERROR_TEXTS, LIST_TEXTS } from '../../core/ui-texts';
import { DocumentListComponent } from '../document-list/document-list';

/** Pantalla "Mis documentos". */
@Component({
  selector: 'app-documents-page',
  imports: [DocumentListComponent],
  template: `
    <section class="page" aria-labelledby="page-title">
      <h1 id="page-title">{{ texts.pageTitle }}</h1>
      <app-document-list
        [documents]="documents()"
        [loading]="loading()"
        [error]="error()"
        (retry)="load()"
      />
    </section>
  `,
  styleUrl: './page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentsPage {
  private readonly api = inject(DocumentsApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly texts = LIST_TEXTS;
  protected readonly documents = signal<readonly DocumentListItem[] | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (documents) => {
          this.documents.set(documents);
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.error.set(error instanceof ApiError ? error.message : ERROR_TEXTS.generic);
          this.loading.set(false);
        },
      });
  }
}
