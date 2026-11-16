// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Router } from '@angular/router';
import { PublicConfigService } from '../../core/config/public-config.service';
import { DocumentSubmissionStore } from '../../core/documents/document-submission.store';
import { DocumentSubmission } from '../../core/documents/document.models';
import { APP_TEXTS, PROGRESS_TEXTS, UPLOAD_TEXTS } from '../../core/ui-texts';
import { ProgressStepsComponent } from '../progress-steps/progress-steps';
import { UploadFormComponent } from '../upload-form/upload-form';

/** Pantalla "Nuevo documento": formulario, progreso y, al terminar, paso a la vista del documento. */
@Component({
  selector: 'app-new-document-page',
  imports: [MatButtonModule, MatCardModule, ProgressStepsComponent, UploadFormComponent],
  providers: [DocumentSubmissionStore],
  templateUrl: './new-document-page.html',
  styleUrl: './page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewDocumentPage {
  protected readonly store = inject(DocumentSubmissionStore);
  private readonly router = inject(Router);
  protected readonly turnstileSiteKey = inject(PublicConfigService).turnstileSiteKey;

  protected readonly texts = UPLOAD_TEXTS;
  protected readonly progressTexts = PROGRESS_TEXTS;
  protected readonly draftNotice = APP_TEXTS.draftNotice;

  /** El formulario se muestra hasta que el servidor acepta el documento, o si lo rechaza para corregirlo. */
  protected readonly showForm = computed(() => this.store.documentId() === null);

  constructor() {
    effect(() => {
      const id = this.store.documentId();
      if (this.store.phase() === 'ready' && id !== null) {
        void this.router.navigate(['/documentos', id]);
      }
    });
  }

  protected submit(submission: DocumentSubmission): void {
    this.store.submit(submission);
  }
}
