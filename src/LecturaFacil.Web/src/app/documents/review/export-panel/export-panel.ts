// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  input,
  output,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { EXPORT_FORMATS, ExportFormat } from '../../../core/documents/document.models';
import { EXPORT_FORMAT_LABELS, REVIEW_TEXTS } from '../../../core/ui-texts';

/**
 * Botón "Exportar" de la barra del documento: abre un menú con los formatos y cada opción descarga el fichero. Solo se
 * activa cuando todos los avisos de datos clave están revisados; el fichero lo genera el servidor.
 */
@Component({
  selector: 'app-export-panel',
  imports: [MatButtonModule, MatMenuModule],
  templateUrl: './export-panel.html',
  // Sin encapsulación: el menú se pinta en una capa fuera del componente.
  encapsulation: ViewEncapsulation.None,
  styles: `
    app-export-panel {
      display: inline-flex;
      flex: none;
    }
    app-export-panel .export-button {
      white-space: nowrap;
    }
    app-export-panel .export-button svg {
      inline-size: 18px;
      block-size: 18px;
      margin-inline-end: 0.375rem;
      fill: none;
      stroke: currentColor;
      stroke-width: 1.8;
      stroke-linecap: round;
      stroke-linejoin: round;
    }
    .export-menu-hint {
      max-inline-size: 16rem;
      margin: 0.25rem 0 0;
      padding: 0.625rem 1rem 0.375rem;
      border-block-start: 1px solid var(--lf-rule);
      color: var(--lf-muted);
      font-size: 0.8125rem;
      line-height: 1.4;
    }
  `,
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

  protected submit(format: ExportFormat): void {
    if (this.canExport() && !this.exporting()) {
      this.exportAdaptation.emit(format);
    }
  }
}
