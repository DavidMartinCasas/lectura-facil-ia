// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  InjectionToken,
  computed,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router, RouterLink } from '@angular/router';
import { PublicConfigService } from '../../core/config/public-config.service';
import { DocumentSubmissionStore } from '../../core/documents/document-submission.store';
import { DocumentSubmission } from '../../core/documents/document.models';
import { APP_TEXTS, PROGRESS_TEXTS, UPLOAD_TEXTS } from '../../core/ui-texts';
import { ProgressStepsComponent } from '../progress-steps/progress-steps';
import { UploadFormComponent } from '../upload-form/upload-form';

/** Cuánto se queda a la vista "Documento listo" antes de abrir el documento. Se puede poner a 0 en los tests. */
export const READY_REDIRECT_MS = new InjectionToken<number>('READY_REDIRECT_MS', {
  providedIn: 'root',
  factory: () => 2_000,
});

/**
 * Pantalla "Nuevo documento". Al enviar, el progreso se ve en un panel encima del formulario, que se queda detrás,
 * atenuado y bloqueado. Al terminar, el panel dice que está listo y abre el documento poco después (o al pulsar
 * "Ver el documento"). Si falla, se puede reintentar o volver al formulario con el texto tal como estaba.
 */
@Component({
  selector: 'app-new-document-page',
  imports: [MatButtonModule, RouterLink, ProgressStepsComponent, UploadFormComponent],
  providers: [DocumentSubmissionStore],
  templateUrl: './new-document-page.html',
  styleUrl: './page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewDocumentPage {
  protected readonly store = inject(DocumentSubmissionStore);
  private readonly router = inject(Router);
  private readonly redirectMs = inject(READY_REDIRECT_MS);
  protected readonly turnstileSiteKey = inject(PublicConfigService).turnstileSiteKey;

  protected readonly texts = UPLOAD_TEXTS;
  protected readonly progressTexts = PROGRESS_TEXTS;
  protected readonly draftNotice = APP_TEXTS.draftNotice;

  /** El panel de progreso se ve en cuanto el servidor acepta el documento; si lo rechaza, se sigue en el formulario. */
  protected readonly tracking = computed(() => this.store.documentId() !== null);

  protected readonly panelTitle = computed(() => {
    switch (this.store.phase()) {
      case 'ready':
        return PROGRESS_TEXTS.readyTitle;
      case 'failed':
        return PROGRESS_TEXTS.failedTitle;
      default:
        return PROGRESS_TEXTS.workingTitle;
    }
  });

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  constructor() {
    // Al abrirse el panel, el foco pasa a él: el formulario de detrás queda bloqueado.
    effect(() => {
      this.panel()?.nativeElement.focus({ preventScroll: true });
    });

    let redirect: ReturnType<typeof setTimeout> | undefined;
    effect(() => {
      clearTimeout(redirect);
      const id = this.store.documentId();
      if (this.store.phase() === 'ready' && id !== null) {
        redirect = setTimeout(
          () => void this.router.navigate(['/documentos', id]),
          this.redirectMs,
        );
      }
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(redirect));
  }

  protected submit(submission: DocumentSubmission): void {
    this.store.submit(submission);
  }
}
