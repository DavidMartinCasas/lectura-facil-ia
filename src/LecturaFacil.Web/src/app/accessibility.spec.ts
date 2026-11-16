// SPDX-License-Identifier: Apache-2.0
// Comprobación automática de accesibilidad (axe-core, reglas WCAG 2.x A y AA) de las tres pantallas de la US-12.
// jsdom no calcula estilos, así que el contraste de color queda para la revisión manual y el E2E.
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import axe from 'axe-core';
import { NEVER, of } from 'rxjs';
import { DocumentEventsService } from './core/documents/document-events.service';
import { DocumentDetails, DocumentListItem } from './core/documents/document.models';
import { DocumentsApiService } from './core/documents/documents-api.service';
import { DocumentPage } from './documents/pages/document-page';
import { DocumentsPage } from './documents/pages/documents-page';
import { NewDocumentPage } from './documents/pages/new-document-page';

const documents: readonly DocumentListItem[] = [
  {
    id: 'doc-1',
    title: 'Bases de la convocatoria',
    status: 'Draft',
    createdAt: '2026-11-16T10:00:00Z',
    updatedAt: '2026-11-16T10:05:00Z',
    provider: 'Servidor local',
  },
];

const processing: DocumentDetails = {
  id: 'doc-1',
  title: 'Bases de la convocatoria',
  status: 'Processing',
  createdAt: '2026-11-16T10:00:00Z',
  updatedAt: '2026-11-16T10:00:00Z',
  error: null,
  adaptation: null,
};

async function violations<T>(
  component: Type<T>,
  inputs: Record<string, unknown> = {},
): Promise<string[]> {
  const fixture = TestBed.createComponent(component);
  for (const [name, value] of Object.entries(inputs)) {
    fixture.componentRef.setInput(name, value);
  }
  await fixture.whenStable();
  document.body.appendChild(fixture.nativeElement as HTMLElement);

  const results = await axe.run(fixture.nativeElement as HTMLElement, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
    rules: { 'color-contrast': { enabled: false } },
  });
  (fixture.nativeElement as HTMLElement).remove();
  return results.violations.map((violation) => `${violation.id}: ${violation.help}`);
}

describe('accessibility (axe)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: DocumentsApiService,
          useValue: {
            list: () => of(documents),
            get: () => of(processing),
            submit: () => of({ id: 'doc-1', status: 'Received' }),
          },
        },
        { provide: DocumentEventsService, useValue: { watch: () => NEVER } },
      ],
    });
  });

  it('"Nuevo documento" has no violations', async () => {
    expect(await violations(NewDocumentPage)).toEqual([]);
  });

  it('"Mis documentos" has no violations', async () => {
    expect(await violations(DocumentsPage)).toEqual([]);
  });

  it('the document view has no violations', async () => {
    expect(await violations(DocumentPage, { id: 'doc-1' })).toEqual([]);
  });
});
