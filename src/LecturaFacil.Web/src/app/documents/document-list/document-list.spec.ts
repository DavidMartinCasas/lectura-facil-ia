// SPDX-License-Identifier: Apache-2.0
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DocumentListItem } from '../../core/documents/document.models';
import { LIST_TEXTS } from '../../core/ui-texts';
import { DocumentListComponent } from './document-list';

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
    },
    {
      id: 'doc-1',
      title: 'Bases',
      status: 'Processing',
      createdAt: '2026-11-15T09:30:00Z',
      updatedAt: '2026-11-15T09:30:00Z',
      provider: null,
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

  it('shows title, date, status and provider of each document', async () => {
    const element = await render({ documents: items });

    const headers = Array.from(element.querySelectorAll('th')).map((cell) =>
      cell.textContent?.trim(),
    );
    expect(headers).toEqual(['Título', 'Fecha', 'Estado', 'Proveedor']);
    const rows = Array.from(element.querySelectorAll('tr[mat-row]')).map((row) =>
      Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent?.trim()),
    );
    expect(element.querySelector('tr[mat-row] .title-link')?.textContent?.trim()).toBe(
      'Convocatoria de ayudas',
    );
    expect(rows[0][1]).toContain('2026');
    expect(rows[0][2]).toBe('Borrador');
    expect(rows[0][3]).toBe('Servidor local');
    expect(rows[1][2]).toBe('Adaptando');
    expect(rows[1][3]).toBe(LIST_TEXTS.noProvider);
    expect(element.querySelector('td a')?.getAttribute('href')).toBe('/documentos/doc-2');
  });

  it('renders titles as text, never as HTML', async () => {
    const element = await render({
      documents: [{ ...items[0], title: '<img src=x onerror=alert(1)>' }],
    });

    expect(element.querySelector('td img')).toBeNull();
    expect(element.querySelector('td a')?.textContent).toBe('<img src=x onerror=alert(1)>');
  });
});
