// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS, PROGRESS_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import { DocumentSubmissionStore } from './document-submission.store';
import { DocumentStatus, DocumentSubmission, SubmitDocumentResponse } from './document.models';
import { DocumentsApiService } from './documents-api.service';

describe('DocumentSubmissionStore', () => {
  const text: DocumentSubmission = { kind: 'text', text: 'Texto de prueba' };
  let submit: ReturnType<
    typeof vi.fn<(submission: DocumentSubmission) => Observable<SubmitDocumentResponse>>
  >;
  let statuses: Subject<DocumentStatus>;
  let watch: ReturnType<typeof vi.fn<(id: string) => Observable<DocumentStatus>>>;
  let store: DocumentSubmissionStore;

  beforeEach(() => {
    statuses = new Subject<DocumentStatus>();
    watch = vi.fn(() => statuses.asObservable());
    submit = vi.fn(() => of<SubmitDocumentResponse>({ id: 'doc-1', status: 'Received' }));
    TestBed.configureTestingModule({
      providers: [
        DocumentSubmissionStore,
        { provide: DocumentsApiService, useValue: { submit } },
        { provide: DocumentEventsService, useValue: { watch: (id: string) => watch(id) } },
      ],
    });
    store = TestBed.inject(DocumentSubmissionStore);
  });

  it('starts idle, without steps', () => {
    expect(store.phase()).toBe('idle');
    expect(store.steps()).toBeNull();
  });

  it('follows the progress of the submitted document until it is ready', () => {
    store.submit(text);

    expect(submit).toHaveBeenCalledWith(text);
    expect(store.phase()).toBe('tracking');
    expect(store.documentId()).toBe('doc-1');
    expect(store.steps()?.[0].state).toBe('current');

    statuses.next('Processing');
    expect(store.steps()?.[1].state).toBe('current');

    statuses.next('Draft');
    statuses.complete();
    expect(store.phase()).toBe('ready');
    expect(store.steps()?.every((step) => step.state === 'done')).toBe(true);
  });

  it('fails with a clear message when the adaptation ends in error', () => {
    store.submit(text);

    statuses.next('Error');
    statuses.complete();

    expect(store.phase()).toBe('failed');
    expect(store.error()).toBe(PROGRESS_TEXTS.adaptationFailed);
  });

  it('shows the translated message when the API rejects the submission', () => {
    submit.mockReturnValue(
      throwError(() => new ApiError(ERROR_TEXTS.scannedPdf, 422, 'document.scanned_pdf')),
    );

    store.submit(text);

    expect(store.phase()).toBe('failed');
    expect(store.error()).toBe(ERROR_TEXTS.scannedPdf);
  });

  it('shows a generic message for unexpected errors', () => {
    submit.mockReturnValue(throwError(() => new Error('TypeError: boom')));

    store.submit(text);

    expect(store.error()).toBe(ERROR_TEXTS.generic);
  });

  it('retries the last submission', () => {
    submit.mockReturnValueOnce(throwError(() => new ApiError(ERROR_TEXTS.unavailable, 503, null)));
    store.submit(text);

    store.retry();

    expect(submit).toHaveBeenCalledTimes(2);
    expect(submit).toHaveBeenLastCalledWith(text);
    expect(store.phase()).toBe('tracking');
    expect(store.error()).toBeNull();
  });

  it('follows the accepted document again on retry, without sending it twice', () => {
    store.submit(text);
    statuses.error(new ApiError(ERROR_TEXTS.progressLost, 0, null));
    expect(store.phase()).toBe('failed');
    statuses = new Subject<DocumentStatus>();

    store.retry();
    statuses.next('Draft');
    statuses.complete();

    expect(submit).toHaveBeenCalledTimes(1);
    expect(watch).toHaveBeenCalledTimes(2);
    expect(watch).toHaveBeenLastCalledWith('doc-1');
    expect(store.phase()).toBe('ready');
    expect(store.error()).toBeNull();
  });

  it('goes back to idle on reset and stops following the document', () => {
    store.submit(text);

    store.reset();
    statuses.next('Draft');

    expect(store.phase()).toBe('idle');
    expect(store.status()).toBeNull();
  });
});
