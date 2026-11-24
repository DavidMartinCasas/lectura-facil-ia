// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { ValidationOutcome, ValidationResult } from '../../../core/documents/document.models';
import { VALIDATION_TEXTS } from '../../../core/ui-texts';

/** Mismos límites que la API (ValidationRecord). */
export const VALIDATED_BY_MAX_LENGTH = 200;
export const NOTES_MAX_LENGTH = 2000;
export const MAX_PARTICIPANTS = 99;

/** Lo que el diálogo necesita de la versión que leyó el grupo. */
export interface ValidationResultDialogData {
  /** Preguntas de las secciones, en orden. */
  readonly sections: readonly string[];
  readonly hasGlossary: boolean;
}

/**
 * Formulario para anotar lo que decidió el grupo de validación: si lo entiende, qué partes le costó entender, quién era,
 * cuántas personas y sus observaciones. Se cierra con el resultado; la página lo guarda con su store.
 */
@Component({
  selector: 'app-validation-result-dialog',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
  ],
  templateUrl: './validation-result-dialog.html',
  styleUrl: './validation-result-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ValidationResultDialogComponent {
  protected readonly data = inject<ValidationResultDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ValidationResultDialogComponent, ValidationResult>>(MatDialogRef);

  protected readonly texts = VALIDATION_TEXTS;
  protected readonly outcomes: readonly ValidationOutcome[] = ['Approved', 'ChangesRequested'];
  protected readonly validatedByMaxLength = VALIDATED_BY_MAX_LENGTH;
  protected readonly notesMaxLength = NOTES_MAX_LENGTH;
  protected readonly maxParticipants = MAX_PARTICIPANTS;

  protected readonly outcome = signal<ValidationOutcome | null>(null);
  protected readonly validatedBy = signal('');
  protected readonly participants = signal('');
  protected readonly notes = signal('');
  protected readonly hardSections = signal<ReadonlySet<number>>(new Set());
  protected readonly hardGlossary = signal(false);
  /** Se ha intentado guardar: desde entonces se enseñan los campos que faltan. */
  protected readonly submitted = signal(false);

  protected readonly outcomeMissing = computed(() => this.submitted() && this.outcome() === null);
  protected readonly validatedByMissing = computed(
    () => this.submitted() && this.validatedBy().trim() === '',
  );
  /** Un número entero entre 1 y 99; `null` si lo escrito no lo es. */
  private readonly participantCount = computed(() => {
    const value = this.participants().trim();
    const count = /^\d{1,2}$/.test(value) ? Number(value) : NaN;
    return count >= 1 && count <= MAX_PARTICIPANTS ? count : null;
  });
  protected readonly participantsInvalid = computed(
    () => this.submitted() && this.participantCount() === null,
  );
  protected readonly asksForChanges = computed(() => this.outcome() === 'ChangesRequested');

  protected onValidatedByInput(event: Event): void {
    this.validatedBy.set((event.target as HTMLInputElement).value);
  }

  protected onParticipantsInput(event: Event): void {
    this.participants.set((event.target as HTMLInputElement).value);
  }

  protected onNotesInput(event: Event): void {
    this.notes.set((event.target as HTMLTextAreaElement).value);
  }

  protected toggleSection(index: number, checked: boolean): void {
    const sections = new Set(this.hardSections());
    if (checked) {
      sections.add(index);
    } else {
      sections.delete(index);
    }
    this.hardSections.set(sections);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.submitted.set(true);
    const outcome = this.outcome();
    const validatedBy = this.validatedBy().trim();
    const participants = this.participantCount();
    if (outcome === null || validatedBy === '' || participants === null) {
      return;
    }
    const notes = this.notes().trim();
    const changes = outcome === 'ChangesRequested';
    this.dialogRef.close({
      outcome,
      validatedBy,
      participants,
      hardSections: changes ? [...this.hardSections()].sort((left, right) => left - right) : [],
      hardGlossary: changes && this.hardGlossary(),
      notes: notes === '' ? null : notes,
    });
  }
}
