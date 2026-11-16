// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatRadioModule } from '@angular/material/radio';
import { EXPORT_FORMATS, ExportFormat } from '../../../core/documents/document.models';
import { EXPORT_FORMAT_LABELS, REVIEW_TEXTS } from '../../../core/ui-texts';

/**
 * Elección del formato de exportación (DOCX por defecto) y botón "Exportar". El botón solo se activa cuando todos los
 * avisos de datos clave están revisados; el fichero lo genera el servidor.
 */
@Component({
  selector: 'app-export-panel',
  imports: [MatButtonModule, MatRadioModule],
  templateUrl: './export-panel.html',
  styleUrl: '../panel.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExportPanelComponent {
  readonly canExport = input.required<boolean>();
  readonly blockedMessage = input('');
  readonly exporting = input(false);
  readonly exportAdaptation = output<ExportFormat>();

  protected readonly texts = REVIEW_TEXTS;
  protected readonly formats = EXPORT_FORMATS;
  protected readonly labels = EXPORT_FORMAT_LABELS;
  protected readonly format = signal<ExportFormat>('docx');

  /** El valor del grupo de botones no tiene tipo: solo se acepta un formato de la lista. */
  protected select(value: unknown): void {
    const format = EXPORT_FORMATS.find((option) => option === value);
    if (format) {
      this.format.set(format);
    }
  }

  protected submit(): void {
    if (this.canExport() && !this.exporting()) {
      this.exportAdaptation.emit(this.format());
    }
  }
}
