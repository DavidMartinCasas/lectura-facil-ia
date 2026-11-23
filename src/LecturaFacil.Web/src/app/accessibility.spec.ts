// SPDX-License-Identifier: Apache-2.0
// Comprobación automática de accesibilidad (axe-core, reglas WCAG 2.x A y AA) de las pantallas de la US-12 y de la
// revisión lado a lado de la US-13, y de la pantalla de login de la US-15.
// jsdom no calcula estilos, así que el contraste de color queda para la revisión manual y el E2E.
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import axe from 'axe-core';
import { NEVER, of } from 'rxjs';
import { LoginPage } from './auth/login-page';
import { AuthService } from './core/auth/auth.service';
import { DocumentEventsService } from './core/documents/document-events.service';
import { DocumentDetails, DocumentListItem } from './core/documents/document.models';
import { DocumentsApiService } from './core/documents/documents-api.service';
import { DocumentPage } from './documents/pages/document-page';
import { DocumentsPage } from './documents/pages/documents-page';
import { NewDocumentPage } from './documents/pages/new-document-page';
import { documentFixture } from './testing/document-fixtures';

const documents: readonly DocumentListItem[] = [
  {
    id: 'doc-1',
    title: 'Bases de la convocatoria',
    status: 'Draft',
    createdAt: '2026-11-16T10:00:00Z',
    updatedAt: '2026-11-16T10:05:00Z',
    provider: 'Servidor local',
    warningCount: 0,
    readability: null,
  },
];

const processing: DocumentDetails = documentFixture('Processing');

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
        { provide: AuthService, useValue: { loadSession: () => of(false) } },
      ],
    });
  });

  it('the login screen has no violations', async () => {
    expect(await violations(LoginPage)).toEqual([]);
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

  it('the side-by-side review of a draft has no violations', async () => {
    TestBed.overrideProvider(DocumentsApiService, {
      useValue: { get: () => of(documentFixture('Draft')) },
    });

    expect(await violations(DocumentPage, { id: 'doc-1' })).toEqual([]);
  });
});
