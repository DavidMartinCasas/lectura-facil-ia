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
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import {
  ReviewableDocument,
  ValidationOutcome,
  ValidationRecord,
  ValidationResult,
} from '../../../core/documents/document.models';
import { VALIDATION_TEXTS } from '../../../core/ui-texts';

const DAY_FORMAT = new Intl.DateTimeFormat('es-ES', { dateStyle: 'long' });

/** Mismos límites que la API (ValidationRecord). */
export const VALIDATED_BY_MAX_LENGTH = 200;
export const NOTES_MAX_LENGTH = 2000;
export const MAX_PARTICIPANTS = 99;

/**
 * Validación humana de un documento adaptado. La hacen personas del grupo destinatario fuera de la aplicación (suelen
 * leerlo impreso); aquí la misma persona que lo adaptó lo envía a validación, anota lo que decidió el grupo y publica la
 * versión validada. Enseña solo el paso que toca según el estado; las acciones las hace la página con su store.
 */
@Component({
  selector: 'app-validation-panel',
  imports: [MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatRadioModule],
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
  protected readonly maxParticipants = MAX_PARTICIPANTS;

  protected readonly outcome = signal<ValidationOutcome | null>(null);
  protected readonly validatedBy = signal('');
  protected readonly participants = signal('');
  protected readonly notes = signal('');
  /** Secciones marcadas como difíciles; se vacían cuando llega otra versión. */
  protected readonly hardSections = linkedSignal<number, ReadonlySet<number>>({
    source: () => this.document().adaptation.version,
    computation: () => new Set<number>(),
  });
  protected readonly hardGlossary = linkedSignal<number, boolean>({
    source: () => this.document().adaptation.version,
    computation: () => false,
  });
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

  protected readonly validation = computed(() => this.document().validation);
  /** La última validación pidió cambios en una versión anterior o en esta: se recuerda en el borrador. */
  protected readonly changesRequested = computed(() => {
    const validation = this.validation();
    return this.document().status === 'Draft' && validation?.outcome === 'ChangesRequested'
      ? validation
      : null;
  });

  /** El grupo pidió cambios en la versión en pantalla: hay que cambiarla antes de volver a enviarla. */
  protected readonly awaitsChanges = computed(() => {
    const previous = this.changesRequested();
    return previous !== null && previous.version === this.document().adaptation.version;
  });
  protected readonly sendBlockedReason = computed(() =>
    this.awaitsChanges() ? this.texts.sendUnchanged : this.texts.sendBlocked,
  );
  /** El historial solo aporta algo cuando hay más de una ronda. */
  protected readonly rounds = computed(() =>
    this.document().validations.length > 1 ? this.document().validations : [],
  );

  protected formatDate(value: string): string {
    return DAY_FORMAT.format(new Date(value));
  }

  /** Quién lo validó y, si se sabe, cuántas personas eran. */
  protected who(validation: ValidationRecord): string {
    return validation.validatedBy + this.texts.people(validation.participants);
  }

  protected hardPartCount(validation: ValidationRecord): number {
    return validation.hardSections.length + (validation.hardGlossary ? 1 : 0);
  }

  protected onParticipantsInput(event: Event): void {
    this.participants.set((event.target as HTMLInputElement).value);
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
    const participants = this.participantCount();
    if (outcome === null || validatedBy === '' || participants === null || this.saving()) {
      return;
    }
    const notes = this.notes().trim();
    const changes = outcome === 'ChangesRequested';
    this.record.emit({
      outcome,
      validatedBy,
      participants,
      hardSections: changes ? [...this.hardSections()].sort((left, right) => left - right) : [],
      hardGlossary: changes && this.hardGlossary(),
      notes: notes === '' ? null : notes,
    });
  }
}
