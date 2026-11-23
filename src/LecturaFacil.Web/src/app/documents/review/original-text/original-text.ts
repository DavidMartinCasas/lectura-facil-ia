// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
} from '@angular/core';
import { OriginalFinding } from '../../../core/documents/document.models';
import { TextSegment, findFragment, segmentText } from '../../../core/documents/text-segments';
import { REVIEW_TEXTS, SEVERITY_LABELS } from '../../../core/ui-texts';

/**
 * Texto original con los fragmentos que incumplen alguna pauta marcados. El marcado se pinta con trozos de texto
 * (interpolación), nunca con HTML. Cada fragmento marcado es un botón: se selecciona con ratón o teclado y muestra
 * la pauta que incumple y la sugerencia. Además, puede señalar un fragmento (`spotlight`, por ejemplo el valor de un
 * dato clave): se recuadra y se desplaza hasta él.
 */
@Component({
  selector: 'app-original-text',
  templateUrl: './original-text.html',
  styleUrl: './original-text.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OriginalTextComponent {
  readonly text = input.required<string>();
  readonly findings = input.required<readonly OriginalFinding[]>();
  /** Id único del título, por si la vista se pinta en pestañas y en columnas. */
  readonly headingId = input('original-title');
  /** Fragmento que se señala en el texto, o `null`. */
  readonly spotlight = input<string | null>(null);

  protected readonly texts = REVIEW_TEXTS;
  protected readonly severityLabels = SEVERITY_LABELS;
  private readonly spot = computed(() => {
    const fragment = this.spotlight();
    return fragment === null ? null : findFragment(this.text(), fragment);
  });
  /** Los rangos de las pautas y, al final, el del fragmento señalado. */
  protected readonly segments = computed(() => {
    const spot = this.spot();
    const findings: readonly { start: number; end: number }[] = this.findings();
    return segmentText(this.text(), spot === null ? findings : [...findings, spot]);
  });
  /** Trozo seleccionado; se olvida si cambia el texto. */
  protected readonly selected = linkedSignal<readonly TextSegment[], TextSegment | null>({
    source: this.segments,
    computation: () => null,
  });
  protected readonly selectedFindings = computed(() => {
    const segment = this.selected();
    const findings = this.findings();
    return segment ? this.findingIndexes(segment).map((index) => findings[index]) : [];
  });

  constructor() {
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const injector = inject(Injector);
    // Al señalar un fragmento, se lleva a la vista.
    effect(() => {
      if (this.spot() === null) {
        return;
      }
      afterNextRender(
        () => {
          const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
          host
            .querySelector('.spot')
            ?.scrollIntoView?.({ block: 'center', behavior: reduced ? 'auto' : 'smooth' });
        },
        { injector },
      );
    });
  }

  /** Si el trozo incumple alguna pauta (y es, por tanto, un botón). */
  protected hasFindings(segment: TextSegment): boolean {
    return this.findingIndexes(segment).length > 0;
  }

  /** Si el trozo es parte del fragmento señalado. */
  protected isSpot(segment: TextSegment): boolean {
    return this.spot() !== null && segment.ranges.includes(this.findings().length);
  }

  private findingIndexes(segment: TextSegment): readonly number[] {
    const count = this.findings().length;
    return segment.ranges.filter((index) => index < count);
  }

  protected select(segment: TextSegment): void {
    this.selected.set(this.selected() === segment ? null : segment);
  }

  /** La barra espaciadora activa el fragmento como un botón, sin desplazar la página. */
  protected selectWithSpace(event: Event, segment: TextSegment): void {
    event.preventDefault();
    this.select(segment);
  }

  protected clear(): void {
    this.selected.set(null);
  }

  protected label(segment: TextSegment): string {
    const count = this.findingIndexes(segment).length;
    return `${segment.text} (${count} ${count === 1 ? 'problema' : 'problemas'})`;
  }
}
