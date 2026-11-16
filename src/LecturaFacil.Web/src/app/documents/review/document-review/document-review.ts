// SPDX-License-Identifier: Apache-2.0
import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatTabsModule } from '@angular/material/tabs';
import { map } from 'rxjs';
import { ManualEdit, ReviewableDocument } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';
import { AdaptedTextComponent } from '../adapted-text/adapted-text';
import { GuidelinesPanelComponent } from '../guidelines-panel/guidelines-panel';
import { KeyFactsPanelComponent } from '../key-facts-panel/key-facts-panel';
import { OriginalTextComponent } from '../original-text/original-text';
import { ReadabilityPanelComponent } from '../readability-panel/readability-panel';

/** Por debajo de este ancho, original y adaptación van en pestañas en lugar de en columnas. */
export const SMALL_SCREEN_QUERY = '(max-width: 47.99rem)';

/**
 * Revisión de un borrador: original y adaptación lado a lado (o en pestañas en pantallas pequeñas) y los paneles de
 * pautas, legibilidad y datos clave. Solo coordina: cada parte es un componente, y las acciones (guardar una edición,
 * marcar un dato revisado) las hace la página a través de su store.
 */
@Component({
  selector: 'app-document-review',
  imports: [
    AdaptedTextComponent,
    GuidelinesPanelComponent,
    KeyFactsPanelComponent,
    MatTabsModule,
    OriginalTextComponent,
    ReadabilityPanelComponent,
  ],
  templateUrl: './document-review.html',
  styleUrl: './document-review.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentReviewComponent {
  readonly document = input.required<ReviewableDocument>();
  readonly saving = input(false);
  readonly save = output<ManualEdit>();
  readonly acknowledge = output<number>();

  protected readonly texts = REVIEW_TEXTS;
  protected readonly smallScreen = toSignal(
    inject(BreakpointObserver)
      .observe(SMALL_SCREEN_QUERY)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );
  protected readonly adaptation = computed(() => this.document().adaptation);
  protected readonly editable = computed(() => this.document().status === 'Draft');
  protected readonly exportMessage = computed(() => {
    const pending = this.adaptation().pendingKeyFactCount;
    return pending > 0 ? REVIEW_TEXTS.exportBlocked(pending) : REVIEW_TEXTS.exportReady;
  });
}
