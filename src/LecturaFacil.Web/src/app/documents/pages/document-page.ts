// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { DocumentViewStore } from '../../core/documents/document-view.store';
import { isAwaitingAdaptation, isReviewable } from '../../core/documents/document.models';
import { statusTone } from '../../core/documents/status-tone';
import {
  APP_TEXTS,
  DOCUMENT_TEXTS,
  REVIEW_TEXTS,
  STATUS_LABELS,
  VALIDATION_TEXTS,
} from '../../core/ui-texts';
import { ProgressStepsComponent } from '../progress-steps/progress-steps';
import { DocumentReviewComponent } from '../review/document-review/document-review';
import { ReadabilityPanelComponent } from '../review/readability-panel/readability-panel';
import { ValidationPanelComponent } from '../review/validation-panel/validation-panel';

/** Vista de un documento: su estado, su progreso y, cuando ya está adaptado, la revisión lado a lado (US-13). */
@Component({
  selector: 'app-document-page',
  imports: [
    DocumentReviewComponent,
    MatButtonModule,
    ProgressStepsComponent,
    ReadabilityPanelComponent,
    RouterLink,
    ValidationPanelComponent,
  ],
  providers: [DocumentViewStore],
  templateUrl: './document-page.html',
  styleUrl: './page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentPage {
  /** Parámetro :id de la ruta. */
  readonly id = input.required<string>();

  protected readonly store = inject(DocumentViewStore);
  protected readonly texts = DOCUMENT_TEXTS;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly draftNotice = APP_TEXTS.draftNotice;
  protected readonly isAwaitingAdaptation = isAwaitingAdaptation;
  protected readonly tone = statusTone;
  protected readonly reviewable = computed(() => {
    const document = this.store.document();
    return document && isReviewable(document) ? document : null;
  });
  protected readonly announcement = computed(() => {
    switch (this.store.lastAction()) {
      case 'saved':
        return REVIEW_TEXTS.saved;
      case 'acknowledged':
        return REVIEW_TEXTS.acknowledgedAnnouncement;
      case 'acknowledgedAll':
        return REVIEW_TEXTS.acknowledgedAllAnnouncement;
      case 'exported':
        return REVIEW_TEXTS.exported;
      case 'retried':
        return DOCUMENT_TEXTS.retriedAnnouncement;
      case 'sent':
        return VALIDATION_TEXTS.sentAnnouncement;
      case 'approved':
        return VALIDATION_TEXTS.approvedAnnouncement;
      case 'changesRequested':
        return VALIDATION_TEXTS.changesAnnouncement;
      case 'withdrawn':
        return VALIDATION_TEXTS.withdrawnAnnouncement;
      case 'published':
        return VALIDATION_TEXTS.publishedAnnouncement;
      default:
        return '';
    }
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.store.load(id));
    });
  }
}
