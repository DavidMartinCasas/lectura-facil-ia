// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, computed, input, linkedSignal } from '@angular/core';
import { OriginalFinding } from '../../../core/documents/document.models';
import { TextSegment, segmentText } from '../../../core/documents/text-segments';
import { REVIEW_TEXTS, SEVERITY_LABELS } from '../../../core/ui-texts';

/**
 * Texto original con los fragmentos que incumplen alguna pauta marcados. El marcado se pinta con trozos de texto
 * (interpolación), nunca con HTML. Cada fragmento marcado es un botón: se selecciona con ratón o teclado y muestra
 * la pauta que incumple y la sugerencia.
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

  protected readonly texts = REVIEW_TEXTS;
  protected readonly severityLabels = SEVERITY_LABELS;
  protected readonly segments = computed(() => segmentText(this.text(), this.findings()));
  /** Trozo seleccionado; se olvida si cambia el texto. */
  protected readonly selected = linkedSignal<readonly TextSegment[], TextSegment | null>({
    source: this.segments,
    computation: () => null,
  });
  protected readonly selectedFindings = computed(() => {
    const segment = this.selected();
    const findings = this.findings();
    return segment ? segment.ranges.map((index) => findings[index]) : [];
  });

  protected select(segment: TextSegment): void {
    this.selected.set(this.selected() === segment ? null : segment);
  }

  protected clear(): void {
    this.selected.set(null);
  }

  protected label(segment: TextSegment): string {
    const count = segment.ranges.length;
    return `${segment.text} (${count} ${count === 1 ? 'problema' : 'problemas'})`;
  }
}
