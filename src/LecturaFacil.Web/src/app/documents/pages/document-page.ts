// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  untracked,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';
import { DocumentViewStore } from '../../core/documents/document-view.store';
import { isAwaitingAdaptation } from '../../core/documents/document.models';
import { APP_TEXTS, DOCUMENT_TEXTS, STATUS_LABELS } from '../../core/ui-texts';
import { ProgressStepsComponent } from '../progress-steps/progress-steps';

/** Vista de un documento: su estado y su progreso. La vista lado a lado llegará con la US-13. */
@Component({
  selector: 'app-document-page',
  imports: [MatButtonModule, MatCardModule, ProgressStepsComponent, RouterLink],
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

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.store.load(id));
    });
  }
}
