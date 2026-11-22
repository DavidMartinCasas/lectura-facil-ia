// SPDX-License-Identifier: Apache-2.0
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import {
  DOCUMENT_STATUSES,
  DocumentListFilter,
  DocumentListItem,
  DocumentStatus,
  isDocumentStatus,
} from '../../core/documents/document.models';
import { DocumentsApiService } from '../../core/documents/documents-api.service';
import { ApiError } from '../../core/errors/api-error';
import { ERROR_TEXTS, LIST_TEXTS, STATUS_LABELS } from '../../core/ui-texts';
import { DocumentListComponent } from '../document-list/document-list';

/** Parámetros de la URL: la búsqueda se puede compartir y sobrevive a "Atrás". */
export const SEARCH_PARAM = 'buscar';
export const STATUS_PARAM = 'estado';

/** Espera tras la última tecla antes de buscar. */
export const SEARCH_DEBOUNCE_MS = 300;

/** Pantalla "Mis documentos": búsqueda por título, filtro por estado y carga por páginas. */
@Component({
  selector: 'app-documents-page',
  imports: [
    DocumentListComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    RouterLink,
  ],
  templateUrl: './documents-page.html',
  styleUrls: ['./page.scss', './documents-page.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentsPage {
  private readonly api = inject(DocumentsApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly texts = LIST_TEXTS;
  protected readonly statusOptions = DOCUMENT_STATUSES;
  protected readonly statusLabels = STATUS_LABELS;

  protected readonly search = signal('');
  protected readonly statuses = signal<readonly DocumentStatus[]>([]);
  protected readonly documents = signal<readonly DocumentListItem[] | null>(null);
  protected readonly nextCursor = signal<string | null>(null);
  /** Primera carga: todavía no hay lista que enseñar. */
  protected readonly loading = signal(false);
  /** Nueva búsqueda con una lista ya en pantalla: se mantiene hasta que llega la nueva. */
  protected readonly refreshing = signal(false);
  protected readonly loadingMore = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly moreError = signal<string | null>(null);

  protected readonly hasFilters = computed(
    () => this.search().trim().length > 0 || this.statuses().length > 0,
  );
  protected readonly summary = computed(() => {
    const items = this.documents();
    return items && items.length > 0
      ? this.texts.shown(items.length, this.nextCursor() !== null)
      : '';
  });

  private request?: Subscription;
  private debounce?: ReturnType<typeof setTimeout>;

  constructor() {
    const params = this.route.snapshot.queryParamMap;
    this.search.set(params.get(SEARCH_PARAM) ?? '');
    this.statuses.set(params.getAll(STATUS_PARAM).filter(isDocumentStatus));

    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.debounce);
      this.request?.unsubscribe();
    });
    this.load();
  }

  protected onSearchInput(value: string): void {
    this.search.set(value);
    clearTimeout(this.debounce);
    this.debounce = setTimeout(() => this.applyFilters(), SEARCH_DEBOUNCE_MS);
  }

  /** Intro en el buscador: busca sin esperar. */
  protected onSearchSubmit(event: Event): void {
    event.preventDefault();
    this.applyFilters();
  }

  protected onStatusesChange(statuses: readonly DocumentStatus[]): void {
    this.statuses.set(statuses);
    this.applyFilters();
  }

  protected clearFilters(): void {
    this.search.set('');
    this.statuses.set([]);
    this.applyFilters();
  }

  /** Primera página con los filtros actuales; también para "Reintentar". */
  protected load(): void {
    this.request?.unsubscribe();
    const hasList = this.documents() !== null && this.error() === null;
    this.loading.set(!hasList);
    this.refreshing.set(hasList);
    this.loadingMore.set(false);
    this.error.set(null);
    this.moreError.set(null);

    this.request = this.api.list(this.filter()).subscribe({
      next: (page) => {
        this.documents.set(page.items);
        this.nextCursor.set(page.nextCursor);
        this.loading.set(false);
        this.refreshing.set(false);
      },
      error: (error: unknown) => {
        this.documents.set(null);
        this.nextCursor.set(null);
        this.error.set(messageOf(error));
        this.loading.set(false);
        this.refreshing.set(false);
      },
    });
  }

  protected loadMore(): void {
    const cursor = this.nextCursor();
    const current = this.documents();
    if (!cursor || !current || this.loadingMore()) {
      return;
    }

    this.request?.unsubscribe();
    this.loadingMore.set(true);
    this.moreError.set(null);
    this.request = this.api.list(this.filter(), cursor).subscribe({
      next: (page) => {
        this.documents.set([...current, ...page.items]);
        this.nextCursor.set(page.nextCursor);
        this.loadingMore.set(false);
        this.focusRow(current.length);
      },
      error: (error: unknown) => {
        this.moreError.set(messageOf(error));
        this.loadingMore.set(false);
      },
    });
  }

  private applyFilters(): void {
    clearTimeout(this.debounce);
    const filter = this.filter();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        [SEARCH_PARAM]: filter.title || null,
        [STATUS_PARAM]: filter.statuses.length > 0 ? filter.statuses : null,
      },
      replaceUrl: true,
    });
    this.load();
  }

  private filter(): DocumentListFilter {
    return { title: this.search().trim(), statuses: this.statuses() };
  }

  /** Tras "Cargar más", el foco pasa al primer documento nuevo: el botón puede desaparecer si no hay más. */
  private focusRow(index: number): void {
    afterNextRender(
      () => {
        const links = this.host.nativeElement.querySelectorAll<HTMLAnchorElement>('.title-link');
        links.item(index)?.focus();
      },
      { injector: this.injector },
    );
  }
}

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : ERROR_TEXTS.generic;
}
