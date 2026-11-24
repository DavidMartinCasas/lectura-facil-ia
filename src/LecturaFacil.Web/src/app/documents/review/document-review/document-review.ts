// SPDX-License-Identifier: Apache-2.0
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { BreakpointObserver } from '@angular/cdk/layout';
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  Injector,
  input,
  linkedSignal,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatTabsModule } from '@angular/material/tabs';
import { map } from 'rxjs';
import {
  HardParts,
  ExportFormat,
  ManualEdit,
  ReviewableDocument,
} from '../../../core/documents/document.models';
import { REVIEW_TEXTS, STATUS_LABELS } from '../../../core/ui-texts';
import { AdaptedTextComponent } from '../adapted-text/adapted-text';
import { ExportPanelComponent } from '../export-panel/export-panel';
import { GuidelinesPanelComponent } from '../guidelines-panel/guidelines-panel';
import { KeyFactsPanelComponent } from '../key-facts-panel/key-facts-panel';
import { OriginalTextComponent } from '../original-text/original-text';

/** Por debajo de este ancho, original y adaptación van en pestañas en lugar de en columnas. */
export const SMALL_SCREEN_QUERY = '(max-width: 47.99rem)';

/** Apartados plegables del panel de la revisión, en este orden. La legibilidad va en la cabecera de la página. */
export type ReviewSection = 'facts' | 'guidelines';

/** Un apartado de la revisión: su nombre, lo que se ve de un vistazo y si tiene avisos. */
interface ReviewPart {
  readonly id: ReviewSection;
  readonly title: string;
  readonly figure: string;
  readonly warning: boolean;
}

/**
 * Revisión de un borrador. Original y adaptación ocupan todo el ancho (o van en pestañas en pantallas pequeñas). La
 * revisión va en un panel lateral que se abre y se cierra desde la barra del documento: datos clave y pautas,
 * plegables y con su recuento a la vista; al entrar se despliegan los que tienen avisos (la legibilidad la enseña la
 * página, junto al título). En
 * pantallas pequeñas el panel se abre por encima de los textos. Desde un dato clave se puede señalar su valor en el
 * original. Solo coordina: cada parte es un componente, y las acciones (guardar una edición, marcar un dato revisado,
 * exportar) las hace la página a través de su store.
 */
@Component({
  selector: 'app-document-review',
  imports: [
    AdaptedTextComponent,
    CdkTrapFocus,
    ExportPanelComponent,
    GuidelinesPanelComponent,
    KeyFactsPanelComponent,
    MatTabsModule,
    OriginalTextComponent,
  ],
  templateUrl: './document-review.html',
  styleUrl: './document-review.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Escape cierra el panel de la revisión si está abierto.
  host: { '(keydown.escape)': 'closePanelOnEscape()' },
})
export class DocumentReviewComponent {
  readonly document = input.required<ReviewableDocument>();
  readonly saving = input(false);
  readonly exporting = input(false);
  readonly save = output<ManualEdit>();
  readonly acknowledge = output<number>();
  readonly acknowledgeAll = output<void>();
  readonly exportAdaptation = output<ExportFormat>();

  protected readonly texts = REVIEW_TEXTS;
  protected readonly smallScreen = toSignal(
    inject(BreakpointObserver)
      .observe(SMALL_SCREEN_QUERY)
      .pipe(map((state) => state.matches)),
    { initialValue: false },
  );
  protected readonly adaptation = computed(() => this.document().adaptation);
  /** Validado o publicado: ya no es un borrador. */
  protected readonly validated = computed(() => {
    const status = this.document().status;
    return status === 'Validated' || status === 'Published';
  });
  /** El sello de la barra: borrador pendiente de validación hasta que se valida; después, su estado. */
  protected readonly stateLabel = computed(() =>
    this.document().status === 'Draft'
      ? this.texts.draftLabel
      : STATUS_LABELS[this.document().status],
  );
  protected readonly editable = computed(() => this.document().editable);
  /** En un borrador al que el grupo pidió cambios, lo que le costó entender. */
  protected readonly hardParts = computed<HardParts | null>(() => {
    const validation = this.document().validation;
    return this.document().status === 'Draft' && validation?.outcome === 'ChangesRequested'
      ? { sections: validation.hardSections, glossary: validation.hardGlossary }
      : null;
  });
  protected readonly exportMessage = computed(() => {
    const pending = this.adaptation().pendingKeyFactCount;
    return pending > 0 ? REVIEW_TEXTS.exportBlocked(pending) : REVIEW_TEXTS.exportReady;
  });

  /** Solo cambia al abrir otro documento, no con cada versión nueva del mismo. */
  private readonly documentId = computed(() => this.document().id);

  protected readonly reviewParts = computed<readonly ReviewPart[]>(() => {
    const adaptation = this.adaptation();
    const guidelines = adaptation.guidelines;
    const pending = adaptation.pendingKeyFactCount;
    const broken = guidelines ? guidelines.totalCount - guidelines.compliantCount : 0;
    return [
      {
        id: 'facts',
        title: REVIEW_TEXTS.keyFactsTitle,
        figure: adaptation.verification
          ? pending > 0
            ? REVIEW_TEXTS.warnings(pending)
            : REVIEW_TEXTS.noWarnings
          : REVIEW_TEXTS.notChecked,
        warning: pending > 0,
      },
      {
        id: 'guidelines',
        title: REVIEW_TEXTS.guidelinesTitle,
        figure: guidelines
          ? REVIEW_TEXTS.guidelinesOf(guidelines.compliantCount, guidelines.totalCount)
          : REVIEW_TEXTS.noReport,
        warning: broken > 0,
      },
    ];
  });

  /**
   * Panel abierto por encima de los textos (pantallas pequeñas): funciona como un diálogo modal. El foco no sale de él
   * y la barra y los textos de detrás quedan inertes hasta cerrarlo.
   */
  protected readonly overlayOpen = computed(() => this.panelOpen() && this.smallScreen());

  /** Datos clave sin revisar: lo que hay que mirar antes de exportar. */
  protected readonly pendingKeyFacts = computed(() => this.adaptation().pendingKeyFactCount);

  /** Pautas sin cumplir: orientativas, no se marcan como revisadas. */
  protected readonly brokenGuidelines = computed(() => {
    const guidelines = this.adaptation().guidelines;
    return guidelines ? guidelines.totalCount - guidelines.compliantCount : 0;
  });

  /** Avisos de toda la revisión, para abrir el panel al entrar. */
  protected readonly warningCount = computed(
    () => this.pendingKeyFacts() + this.brokenGuidelines(),
  );

  /**
   * Panel de la revisión abierto. Al entrar en un documento con avisos se abre solo en pantallas anchas (en las
   * pequeñas taparía los textos). Al guardar o marcar un dato (el mismo documento, otra versión) se queda como estaba.
   */
  protected readonly panelOpen = linkedSignal<string, boolean>({
    source: this.documentId,
    computation: () => untracked(() => !this.smallScreen() && this.warningCount() > 0),
  });

  /**
   * Apartados desplegados. Al abrir otro documento, los que tienen avisos (o los datos clave si no hay ninguno). Sin
   * seguir los recuentos: si no, al marcar un dato como revisado se plegaría.
   */
  protected readonly expanded = linkedSignal<string, ReadonlySet<ReviewSection>>({
    source: this.documentId,
    computation: () =>
      untracked(() => {
        const warned = this.reviewParts()
          .filter((part) => part.warning)
          .map((part) => part.id);
        return new Set<ReviewSection>(warned.length > 0 ? warned : ['facts']);
      }),
  });

  /** Dato clave señalado en el original. */
  protected readonly located = linkedSignal<string, number | null>({
    source: this.documentId,
    computation: () => null,
  });
  protected readonly spotlight = computed(() => {
    const id = this.located();
    const facts = this.adaptation().verification?.keyFacts ?? [];
    return id === null ? null : (facts.find((fact) => fact.id === id)?.originalValue ?? null);
  });
  /** En pantallas pequeñas, la pestaña abierta (0, el original; 1, la adaptación). */
  protected readonly tab = signal(0);

  private readonly toggleButton = viewChild<ElementRef<HTMLButtonElement>>('toggle');
  private readonly closeButton = viewChild<ElementRef<HTMLButtonElement>>('close');
  private readonly injector = inject(Injector);

  protected togglePanel(): void {
    if (this.panelOpen()) {
      this.closePanel();
      return;
    }
    this.panelOpen.set(true);
    if (this.smallScreen()) {
      // Por encima de los textos: el foco entra en el panel.
      afterNextRender(() => this.closeButton()?.nativeElement.focus(), { injector: this.injector });
    }
  }

  protected closePanel(): void {
    const overlay = this.overlayOpen();
    this.panelOpen.set(false);
    // Sin el panel no hay dónde quitar la marca de un dato clave.
    this.located.set(null);
    if (overlay) {
      // Después de pintar: hasta entonces la barra sigue inerte y no admite el foco.
      afterNextRender(() => this.toggleButton()?.nativeElement.focus(), {
        injector: this.injector,
      });
    }
  }

  protected closePanelOnEscape(): void {
    if (this.panelOpen()) {
      this.closePanel();
    }
  }

  protected toggleSection(section: ReviewSection): void {
    const next = new Set(this.expanded());
    if (next.has(section)) {
      next.delete(section);
      if (section === 'facts') {
        this.located.set(null);
      }
    } else {
      next.add(section);
    }
    this.expanded.set(next);
  }

  protected locate(factId: number): void {
    const next = this.located() === factId ? null : factId;
    this.located.set(next);
    if (next !== null && this.smallScreen()) {
      // En pantallas pequeñas el panel tapa los textos: se cierra y se pasa al original para verlo.
      this.panelOpen.set(false);
      this.tab.set(0);
    }
  }
}
