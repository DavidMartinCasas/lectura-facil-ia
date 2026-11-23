// SPDX-License-Identifier: Apache-2.0
import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { DocumentListItem, ReadabilityComparison } from '../../core/documents/document.models';
import { statusTone } from '../../core/documents/status-tone';
import { LIST_TEXTS, STATUS_LABELS } from '../../core/ui-texts';

const DATE_FORMAT = new Intl.DateTimeFormat('es-ES', { dateStyle: 'medium', timeStyle: 'short' });
const DAY_FORMAT = new Intl.DateTimeFormat('es-ES', { dateStyle: 'medium' });
const RELATIVE_FORMAT = new Intl.RelativeTimeFormat('es-ES', { numeric: 'auto' });

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/**
 * Cuándo se modificó, en palabras: "Hace 5 minutos", "Hace 3 horas", "Ayer", "Hace 4 días". A partir de una semana, la
 * fecha ("16 nov 2026"). La fecha y la hora exactas van en el título del elemento.
 */
export function formatRelativeDate(value: string, now: number = Date.now()): string {
  const time = new Date(value).getTime();
  if (Number.isNaN(time)) {
    return '';
  }

  const elapsed = Math.max(0, now - time);
  if (elapsed < MINUTE) {
    return LIST_TEXTS.justNow;
  }

  const relative =
    elapsed < HOUR
      ? RELATIVE_FORMAT.format(-Math.floor(elapsed / MINUTE), 'minute')
      : elapsed < DAY
        ? RELATIVE_FORMAT.format(-Math.floor(elapsed / HOUR), 'hour')
        : elapsed < 7 * DAY
          ? RELATIVE_FORMAT.format(-Math.floor(elapsed / DAY), 'day')
          : DAY_FORMAT.format(time);
  return relative.charAt(0).toUpperCase() + relative.slice(1);
}

/** Tabla de "Mis documentos" con sus estados de carga, vacío y error. No llama a la API: emite "retry" y "clearFilters". */
@Component({
  selector: 'app-document-list',
  imports: [MatButtonModule, MatProgressBarModule, MatTableModule, NgTemplateOutlet, RouterLink],
  templateUrl: './document-list.html',
  styleUrl: './document-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentListComponent {
  readonly documents = input<readonly DocumentListItem[] | null>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  /** Hay búsqueda o filtro de estado: una lista vacía significa "nada coincide", no "no tienes documentos". */
  readonly filtered = input(false);
  readonly retry = output();
  readonly clearFilters = output();

  protected readonly texts = LIST_TEXTS;
  protected readonly tone = statusTone;
  protected readonly columns = [
    'title',
    'status',
    'warnings',
    'readability',
    'updatedAt',
    'provider',
  ] as const;
  protected readonly relativeDate = formatRelativeDate;

  protected statusLabel(item: DocumentListItem): string {
    return STATUS_LABELS[item.status] ?? item.status;
  }

  protected round(value: number): number {
    return Math.round(value);
  }

  protected readabilityLabel(readability: ReadabilityComparison): string {
    return this.texts.readabilityChange(
      Math.round(readability.original.value),
      Math.round(readability.adapted.value),
      readability.adapted.gradeLabel,
    );
  }

  protected formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : DATE_FORMAT.format(date);
  }
}
