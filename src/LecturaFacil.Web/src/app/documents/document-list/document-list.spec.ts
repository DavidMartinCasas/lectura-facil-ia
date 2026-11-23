// SPDX-License-Identifier: Apache-2.0
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DocumentListItem } from '../../core/documents/document.models';
import { LIST_TEXTS } from '../../core/ui-texts';
import { DocumentListComponent, formatRelativeDate } from './document-list';

describe('DocumentListComponent', () => {
  let fixture: ComponentFixture<DocumentListComponent>;

  const items: readonly DocumentListItem[] = [
    {
      id: 'doc-2',
      title: 'Convocatoria de ayudas',
      status: 'Draft',
      createdAt: '2026-11-16T10:00:00Z',
      updatedAt: '2026-11-16T10:05:00Z',
      provider: 'Servidor local',
      pendingKeyFactCount: 3,
      brokenGuidelineCount: 2,
      readability: {
        original: { value: 41.6, grade: 'SomewhatDifficult', gradeLabel: 'Algo difícil' },
        adapted: { value: 72.4, grade: 'QuiteEasy', gradeLabel: 'Bastante fácil' },
        improvement: 30.8,
      },
    },
    {
      id: 'doc-1',
      title: 'Bases',
      status: 'Processing',
      createdAt: '2026-11-15T09:30:00Z',
      updatedAt: '2026-11-15T09:30:00Z',
      provider: null,
      pendingKeyFactCount: null,
      brokenGuidelineCount: null,
      readability: null,
    },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    fixture = TestBed.createComponent(DocumentListComponent);
  });

  async function render(inputs: Record<string, unknown>): Promise<HTMLElement> {
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows a loading state', async () => {
    const element = await render({ loading: true });

    expect(element.querySelector('[role="status"]')?.textContent).toContain(LIST_TEXTS.loading);
    expect(element.querySelector('table')).toBeNull();
  });

  it('shows an empty state with a link to create a document', async () => {
    const element = await render({ documents: [] });

    expect(element.textContent).toContain(LIST_TEXTS.empty);
    expect(element.querySelector('a')?.getAttribute('href')).toBe('/nuevo');
  });

  it('when filtered and empty, says nothing matches and offers to clear the filters', async () => {
    const element = await render({ documents: [], filtered: true });
    let cleared = 0;
    fixture.componentInstance.clearFilters.subscribe(() => cleared++);

    expect(element.textContent).toContain(LIST_TEXTS.noMatches);
    expect(element.textContent).not.toContain(LIST_TEXTS.empty);
    element.querySelector<HTMLButtonElement>('button')!.click();
    expect(cleared).toBe(1);
  });

  it('shows the error with a retry button', async () => {
    const element = await render({ error: 'No se puede conectar.' });
    let retried = 0;
    fixture.componentInstance.retry.subscribe(() => retried++);

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('No se puede conectar.');
    element.querySelector<HTMLButtonElement>('button')!.click();
    expect(retried).toBe(1);
  });

  it('shows title, status, warnings, readability, last change and provider of each document', async () => {
    const element = await render({ documents: items });

    const headers = Array.from(element.querySelectorAll('th')).map((cell) =>
      cell.textContent?.trim(),
    );
    expect(headers).toEqual([
      'Título',
      'Estado',
      'Revisión',
      'Legibilidad',
      'Modificado',
      'Proveedor',
    ]);
    const rows = Array.from(element.querySelectorAll('tr[mat-row]')).map((row) =>
      Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent?.trim()),
    );
    expect(element.querySelector('tr[mat-row] .title-link')?.textContent?.trim()).toBe(
      'Convocatoria de ayudas',
    );
    // En la celda del estado va también, oculta salvo en pantallas estrechas, la etiqueta de avisos.
    expect(rows[0][1]).toMatch(/^Borrador/);
    expect(rows[0][2]).toBe('3 datos por revisar 2 pautas sin cumplir');
    expect(rows[0][3]).toContain('42 → 72');
    expect(rows[0][3]).toContain('Bastante fácil');
    expect(rows[0][4]).toContain('2026');
    expect(rows[0][5]).toBe('Servidor local');
    expect(rows[1][1]).toBe('Adaptando');
    expect(rows[1][2]).toContain(LIST_TEXTS.notAdapted);
    expect(rows[1][3]).toContain(LIST_TEXTS.notAdapted);
    expect(rows[1][5]).toBe(LIST_TEXTS.noProvider);
    expect(element.querySelector('td a')?.getAttribute('href')).toBe('/documentos/doc-2');
  });

  it('says "Revisado" when no key fact is pending, and the broken guidelines apart', async () => {
    const element = await render({
      documents: [
        { ...items[0], pendingKeyFactCount: 0 },
        { ...items[0], id: 'doc-3', pendingKeyFactCount: 0, brokenGuidelineCount: 0 },
      ],
    });

    const cells = [...element.querySelectorAll('td.col-warnings')];
    expect(cells[0].querySelector('.chip')?.textContent?.trim()).toBe(LIST_TEXTS.keyFactsReviewed);
    expect(cells[0].querySelector('.chip--ok')).not.toBeNull();
    expect(cells[0].querySelector('.guidelines')?.textContent?.trim()).toBe('2 pautas sin cumplir');
    expect(cells[1].textContent?.trim()).toBe(LIST_TEXTS.keyFactsReviewed);
  });

  it('tells screen readers the readability in words, not with the arrow', async () => {
    const element = await render({ documents: items });

    const cell = element.querySelector('td.col-readability')!;
    expect(cell.querySelector('[aria-hidden="true"]')?.textContent).toContain('→');
    expect(cell.querySelector('.visually-hidden')?.textContent?.trim()).toBe(
      'De 42 a 72 puntos: Bastante fácil',
    );
  });

  it('keeps the exact date of the last change in the title', async () => {
    const element = await render({ documents: items });

    const time = element.querySelector('td.col-date time')!;
    expect(time.getAttribute('datetime')).toBe('2026-11-16T10:05:00Z');
    expect(time.getAttribute('title')).toContain('2026');
  });

  it('renders titles as text, never as HTML', async () => {
    const element = await render({
      documents: [{ ...items[0], title: '<img src=x onerror=alert(1)>' }],
    });

    expect(element.querySelector('td img')).toBeNull();
    expect(element.querySelector('td a')?.textContent).toBe('<img src=x onerror=alert(1)>');
  });
});

describe('formatRelativeDate', () => {
  const now = Date.UTC(2026, 10, 20, 12, 0);
  const ago = (milliseconds: number): string => new Date(now - milliseconds).toISOString();

  it('says the last change in words up to a week, and the date after that', () => {
    expect(formatRelativeDate(ago(30_000), now)).toBe('Hace un momento');
    expect(formatRelativeDate(ago(5 * 60_000), now)).toBe('Hace 5 minutos');
    expect(formatRelativeDate(ago(3 * 3_600_000), now)).toBe('Hace 3 horas');
    expect(formatRelativeDate(ago(26 * 3_600_000), now)).toBe('Ayer');
    expect(formatRelativeDate(ago(4 * 86_400_000), now)).toBe('Hace 4 días');
    expect(formatRelativeDate(ago(10 * 86_400_000), now)).toContain('2026');
  });

  it('returns nothing for an invalid date', () => {
    expect(formatRelativeDate('no es una fecha', now)).toBe('');
  });
});
