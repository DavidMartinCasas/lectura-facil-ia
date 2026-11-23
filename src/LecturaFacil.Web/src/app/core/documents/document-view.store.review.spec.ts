// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { NEVER, Observable, Subject, of, throwError } from 'rxjs';
import { adaptationFixture, documentFixture } from '../../testing/document-fixtures';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import { DocumentViewStore } from './document-view.store';
import { DocumentDetails, ManualEdit } from './document.models';
import { DocumentsApiService } from './documents-api.service';

const edit: ManualEdit = {
  title: 'Ayudas para el alquiler',
  sections: [{ question: '¿Cuánto dinero dan?', sentences: ['Como mucho, 250 euros al mes.'] }],
  glossary: [],
};

describe('DocumentViewStore (revisión)', () => {
  let saveEdit: ReturnType<
    typeof vi.fn<(id: string, version: number, edit: ManualEdit) => Observable<DocumentDetails>>
  >;
  let acknowledgeKeyFact: ReturnType<
    typeof vi.fn<(id: string, version: number, factId: number) => Observable<DocumentDetails>>
  >;
  let acknowledgeAllKeyFacts: ReturnType<
    typeof vi.fn<(id: string, version: number) => Observable<DocumentDetails>>
  >;
  let store: DocumentViewStore;

  beforeEach(() => {
    acknowledgeAllKeyFacts = vi.fn(() =>
      of(
        documentFixture('Draft', {
          adaptation: adaptationFixture({ pendingKeyFactCount: 0, canExport: true }),
        }),
      ),
    );
    saveEdit = vi.fn(() =>
      of(documentFixture('Draft', { adaptation: adaptationFixture({ version: 2 }) })),
    );
    acknowledgeKeyFact = vi.fn(() =>
      of(
        documentFixture('Draft', {
          adaptation: adaptationFixture({ pendingKeyFactCount: 0, canExport: true }),
        }),
      ),
    );
    TestBed.configureTestingModule({
      providers: [
        DocumentViewStore,
        {
          provide: DocumentsApiService,
          useValue: {
            get: () => of(documentFixture('Draft')),
            saveEdit,
            acknowledgeKeyFact,
            acknowledgeAllKeyFacts,
          },
        },
        { provide: DocumentEventsService, useValue: { watch: () => NEVER } },
      ],
    });
    store = TestBed.inject(DocumentViewStore);
    store.load('doc-1');
  });

  it('saves an edit of the version on screen and shows the recalculated document', () => {
    store.saveEdit(edit);

    expect(saveEdit).toHaveBeenCalledWith('doc-1', 1, edit);
    expect(store.document()?.adaptation?.version).toBe(2);
    expect(store.saving()).toBe(false);
    expect(store.lastAction()).toBe('saved');
  });

  it('acknowledges a key fact of the version on screen', () => {
    store.acknowledgeKeyFact(0);

    expect(acknowledgeKeyFact).toHaveBeenCalledWith('doc-1', 1, 0);
    expect(store.document()?.adaptation?.canExport).toBe(true);
    expect(store.lastAction()).toBe('acknowledged');
  });

  it('acknowledges every key fact of the version on screen at once', () => {
    store.acknowledgeAllKeyFacts();

    expect(acknowledgeAllKeyFacts).toHaveBeenCalledWith('doc-1', 1);
    expect(store.document()?.adaptation?.canExport).toBe(true);
    expect(store.lastAction()).toBe('acknowledgedAll');
  });

  it('ignores a second action while one is in progress', () => {
    const pending = new Subject<DocumentDetails>();
    saveEdit.mockReturnValueOnce(pending);

    store.saveEdit(edit);
    store.acknowledgeKeyFact(0);

    expect(store.saving()).toBe(true);
    expect(acknowledgeKeyFact).not.toHaveBeenCalled();
  });

  it('keeps the document and shows the message when the version has changed', () => {
    saveEdit.mockReturnValueOnce(
      throwError(() => new ApiError(ERROR_TEXTS.conflict, 409, 'review.version_conflict')),
    );

    store.saveEdit(edit);

    expect(store.actionError()).toBe(ERROR_TEXTS.conflict);
    expect(store.document()?.adaptation?.version).toBe(1);
    expect(store.saving()).toBe(false);
  });

  it('shows a generic message for unexpected errors', () => {
    acknowledgeKeyFact.mockReturnValueOnce(throwError(() => new Error('boom')));

    store.acknowledgeKeyFact(0);

    expect(store.actionError()).toBe(ERROR_TEXTS.generic);
  });
});
