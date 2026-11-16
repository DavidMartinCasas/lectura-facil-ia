// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  linkedSignal,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Adaptation, ManualEdit } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';

/**
 * La adaptación (título, secciones con pregunta y frases, y glosario) como texto plano. Cada sección se edita por
 * separado; al guardar se envía la adaptación completa con esa sección cambiada. Las pautas y la legibilidad las
 * recalcula el servidor.
 */
@Component({
  selector: 'app-adapted-text',
  imports: [MatButtonModule],
  templateUrl: './adapted-text.html',
  styleUrl: './adapted-text.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdaptedTextComponent {
  readonly adaptation = input.required<Adaptation>();
  readonly editable = input(false);
  readonly saving = input(false);
  readonly headingId = input('adapted-title');
  readonly save = output<ManualEdit>();

  protected readonly texts = REVIEW_TEXTS;
  /** Sección que se está editando; se cierra cuando llega una versión nueva. */
  protected readonly editing = linkedSignal<number, number | null>({
    source: () => this.adaptation().version,
    computation: () => null,
  });
  protected readonly draftQuestion = signal('');
  protected readonly draftSentences = signal('');
  protected readonly draftValid = computed(
    () => this.draftQuestion().trim().length > 0 && splitLines(this.draftSentences()).length > 0,
  );

  protected edit(index: number): void {
    const section = this.adaptation().sections[index];
    this.draftQuestion.set(section.question);
    this.draftSentences.set(section.sentences.join('\n'));
    this.editing.set(index);
  }

  protected cancel(): void {
    this.editing.set(null);
  }

  protected onQuestionInput(event: Event): void {
    this.draftQuestion.set((event.target as HTMLInputElement).value);
  }

  protected onSentencesInput(event: Event): void {
    this.draftSentences.set((event.target as HTMLTextAreaElement).value);
  }

  protected submit(index: number): void {
    if (!this.draftValid() || this.saving()) {
      return;
    }
    const adaptation = this.adaptation();
    this.save.emit({
      title: adaptation.title,
      sections: adaptation.sections.map((section, position) =>
        position === index
          ? { question: this.draftQuestion().trim(), sentences: splitLines(this.draftSentences()) }
          : section,
      ),
      glossary: adaptation.glossary,
    });
  }
}

/** Una frase por línea, sin líneas vacías. */
function splitLines(text: string): string[] {
  return text
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
}
