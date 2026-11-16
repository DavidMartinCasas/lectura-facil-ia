// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import { DocumentViewStore } from './document-view.store';
import { DocumentDetails, DocumentStatus } from './document.models';
import { DocumentsApiService } from './documents-api.service';

function documentWith(status: DocumentStatus): DocumentDetails {
  return {
    id: 'doc-1',
    title: 'Bases',
    status,
    createdAt: '2026-11-16T10:00:00Z',
    updatedAt: '2026-11-16T10:00:00Z',
    error: null,
    adaptation: status === 'Draft' ? { version: 1, pendingHumanValidation: true } : null,
  };
}

describe('DocumentViewStore', () => {
  let get: ReturnType<typeof vi.fn<(id: string) => Observable<DocumentDetails>>>;
  let statuses: Subject<DocumentStatus>;
  let store: DocumentViewStore;

  beforeEach(() => {
    statuses = new Subject<DocumentStatus>();
    get = vi.fn(() => of(documentWith('Draft')));
    TestBed.configureTestingModule({
      providers: [
        DocumentViewStore,
        { provide: DocumentsApiService, useValue: { get } },
        { provide: DocumentEventsService, useValue: { watch: () => statuses.asObservable() } },
      ],
    });
    store = TestBed.inject(DocumentViewStore);
  });

  it('loads a finished document without following its progress', () => {
    store.load('doc-1');

    expect(get).toHaveBeenCalledWith('doc-1');
    expect(store.document()?.title).toBe('Bases');
    expect(store.status()).toBe('Draft');
    expect(store.loading()).toBe(false);
  });

  it('follows a document that is being adapted and reloads it at the end', () => {
    get.mockReturnValueOnce(of(documentWith('Processing')));

    store.load('doc-1');
    expect(store.steps()?.[1].state).toBe('current');

    statuses.next('Draft');
    statuses.complete();

    expect(get).toHaveBeenCalledTimes(2);
    expect(store.status()).toBe('Draft');
  });

  it('shows the translated message when the document cannot be loaded', () => {
    get.mockReturnValue(
      throwError(() => new ApiError(ERROR_TEXTS.notFound, 404, 'document.not_found')),
    );

    store.load('doc-1');

    expect(store.error()).toBe(ERROR_TEXTS.notFound);
    expect(store.loading()).toBe(false);
  });
});
