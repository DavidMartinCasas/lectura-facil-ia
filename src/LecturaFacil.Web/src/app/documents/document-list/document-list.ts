// SPDX-License-Identifier: Apache-2.0
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { DocumentListItem } from '../../core/documents/document.models';
import { LIST_TEXTS, STATUS_LABELS } from '../../core/ui-texts';

const DATE_FORMAT = new Intl.DateTimeFormat('es-ES', { dateStyle: 'medium', timeStyle: 'short' });

/** Tabla de "Mis documentos" con sus estados de carga, vacío y error. No llama a la API: emite "retry". */
@Component({
  selector: 'app-document-list',
  imports: [MatButtonModule, MatProgressBarModule, MatTableModule, RouterLink],
  templateUrl: './document-list.html',
  styleUrl: './document-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentListComponent {
  readonly documents = input<readonly DocumentListItem[] | null>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly retry = output();

  protected readonly texts = LIST_TEXTS;
  protected readonly columns = ['title', 'createdAt', 'status', 'provider'] as const;

  protected statusLabel(item: DocumentListItem): string {
    return STATUS_LABELS[item.status] ?? item.status;
  }

  protected formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : DATE_FORMAT.format(date);
  }
}
