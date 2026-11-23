// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import {
  ReviewableDocument,
  ValidationOutcome,
  ValidationResult,
} from '../../../core/documents/document.models';
import { VALIDATION_TEXTS } from '../../../core/ui-texts';

const DAY_FORMAT = new Intl.DateTimeFormat('es-ES', { dateStyle: 'long' });

/** Mismos límites que la API (ValidationRecord). */
export const VALIDATED_BY_MAX_LENGTH = 200;
export const NOTES_MAX_LENGTH = 2000;

/**
 * Validación humana de un documento adaptado. La hacen personas del grupo destinatario fuera de la aplicación (suelen
 * leerlo impreso); aquí la misma persona que lo adaptó lo envía a validación, anota lo que decidió el grupo y publica la
 * versión validada. Enseña solo el paso que toca según el estado; las acciones las hace la página con su store.
 */
@Component({
  selector: 'app-validation-panel',
  imports: [MatButtonModule, MatFormFieldModule, MatInputModule, MatRadioModule],
  templateUrl: './validation-panel.html',
  styleUrl: './validation-panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ValidationPanelComponent {
  readonly document = input.required<ReviewableDocument>();
  readonly saving = input(false);
  readonly send = output<void>();
  readonly record = output<ValidationResult>();
  readonly withdraw = output<void>();
  readonly publish = output<void>();

  protected readonly texts = VALIDATION_TEXTS;
  protected readonly outcomes: readonly ValidationOutcome[] = ['Approved', 'ChangesRequested'];
  protected readonly validatedByMaxLength = VALIDATED_BY_MAX_LENGTH;
  protected readonly notesMaxLength = NOTES_MAX_LENGTH;

  protected readonly outcome = signal<ValidationOutcome | null>(null);
  protected readonly validatedBy = signal('');
  protected readonly notes = signal('');
  /** Se ha intentado guardar: desde entonces se enseñan los campos que faltan. */
  protected readonly submitted = signal(false);

  protected readonly outcomeMissing = computed(() => this.submitted() && this.outcome() === null);
  protected readonly validatedByMissing = computed(
    () => this.submitted() && this.validatedBy().trim() === '',
  );

  protected readonly validation = computed(() => this.document().validation);
  /** La última validación pidió cambios en una versión anterior o en esta: se recuerda en el borrador. */
  protected readonly changesRequested = computed(() => {
    const validation = this.validation();
    return this.document().status === 'Draft' && validation?.outcome === 'ChangesRequested'
      ? validation
      : null;
  });

  protected formatDate(value: string): string {
    return DAY_FORMAT.format(new Date(value));
  }

  protected onValidatedByInput(event: Event): void {
    this.validatedBy.set((event.target as HTMLInputElement).value);
  }

  protected onNotesInput(event: Event): void {
    this.notes.set((event.target as HTMLTextAreaElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.submitted.set(true);
    const outcome = this.outcome();
    const validatedBy = this.validatedBy().trim();
    if (outcome === null || validatedBy === '' || this.saving()) {
      return;
    }
    const notes = this.notes().trim();
    this.record.emit({ outcome, validatedBy, notes: notes === '' ? null : notes });
  }
}
