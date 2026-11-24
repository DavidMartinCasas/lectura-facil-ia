// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import {
  DocumentStatus,
  ReviewableDocument,
  ValidationRecord,
  ValidationResult,
} from '../../../core/documents/document.models';
import { VALIDATION_TEXTS } from '../../../core/ui-texts';
import {
  ValidationResultDialogComponent,
  ValidationResultDialogData,
} from './validation-result-dialog';

const DAY_FORMAT = new Intl.DateTimeFormat('es-ES', { dateStyle: 'long' });

/** Los pasos de la validación, en orden. */
const STEPS: readonly DocumentStatus[] = ['Draft', 'PendingValidation', 'Validated', 'Published'];

/**
 * Validación humana de un documento adaptado, como una barra fina encima del texto: en qué paso está, el botón que toca
 * ahora y una línea con lo importante. Lo que pidió el grupo y las rondas se despliegan bajo la barra, y el resultado se
 * anota en un diálogo. La validación la hacen personas del grupo destinatario fuera de la aplicación; aquí la misma
 * persona que lo adaptó la registra. Las acciones las hace la página con su store.
 */
@Component({
  selector: 'app-validation-panel',
  imports: [MatButtonModule],
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

  private readonly dialog = inject(MatDialog);

  protected readonly texts = VALIDATION_TEXTS;
  protected readonly steps = STEPS;
  protected readonly currentStep = computed(() => STEPS.indexOf(this.document().status));
  protected readonly detailsOpen = signal(false);

  protected readonly validation = computed(() => this.document().validation);
  /** La última validación pidió cambios: se recuerda en el borrador mientras se corrige. */
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
  /** La validación que se resume en la barra: la que pidió cambios en un borrador, o la que lo validó. */
  protected readonly shown = computed(() => {
    const status = this.document().status;
    if (status === 'Draft') {
      return this.changesRequested();
    }
    return status === 'Validated' || status === 'Published' ? this.validation() : null;
  });
  protected readonly showsParts = computed(() => {
    const shown = this.shown();
    return this.document().status === 'Draft' && shown !== null && this.hardPartCount(shown) > 0;
  });
  /** Hay algo que desplegar: las partes difíciles, las observaciones o las rondas. */
  protected readonly hasDetails = computed(
    () => this.showsParts() || !!this.shown()?.notes || this.rounds().length > 0,
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

  protected toggleDetails(): void {
    this.detailsOpen.update((open) => !open);
  }

  /** Abre el formulario; si se guarda, la página anota el resultado. */
  protected openRecord(): void {
    const adaptation = this.document().adaptation;
    this.dialog
      .open<ValidationResultDialogComponent, ValidationResultDialogData, ValidationResult>(
        ValidationResultDialogComponent,
        {
          data: {
            sections: adaptation.sections.map((section) => section.question),
            hasGlossary: adaptation.glossary.length > 0,
          },
          width: 'min(40rem, calc(100vw - 2rem))',
          maxWidth: '100vw',
          autoFocus: 'first-tabbable',
          restoreFocus: true,
        },
      )
      .afterClosed()
      .subscribe((result) => {
        if (result) {
          this.record.emit(result);
        }
      });
  }
}
