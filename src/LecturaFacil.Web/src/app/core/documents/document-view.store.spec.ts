// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import { DocumentViewStore } from './document-view.store';
import { DocumentDetails, DocumentStatus } from './document.models';
import { DocumentsApiService } from './documents-api.service';
import { documentFixture } from '../../testing/document-fixtures';

function documentWith(status: DocumentStatus): DocumentDetails {
  return documentFixture(status, { title: 'Bases' });
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

  it('forgets the previous document when another one fails to load', () => {
    store.load('doc-1');
    get.mockReturnValue(
      throwError(() => new ApiError(ERROR_TEXTS.notFound, 404, 'document.not_found')),
    );

    store.load('doc-2');

    expect(store.document()).toBeNull();
    expect(store.status()).toBeNull();
    expect(store.error()).toBe(ERROR_TEXTS.notFound);
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
