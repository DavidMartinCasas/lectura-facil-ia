// SPDX-License-Identifier: Apache-2.0
import { Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import { DocumentEventsService } from '../../core/documents/document-events.service';
import {
  DocumentDetails,
  DocumentListItem,
  DocumentStatus,
  SubmitDocumentResponse,
} from '../../core/documents/document.models';
import { DocumentsApiService } from '../../core/documents/documents-api.service';
import { ApiError } from '../../core/errors/api-error';
import { ERROR_TEXTS, PROGRESS_TEXTS, REVIEW_TEXTS } from '../../core/ui-texts';
import { documentFixture } from '../../testing/document-fixtures';
import { DocumentPage } from './document-page';
import { DocumentsPage } from './documents-page';
import { NewDocumentPage } from './new-document-page';

const details = (status: DocumentStatus): DocumentDetails => documentFixture(status);

describe('pages', () => {
  let statuses: Subject<DocumentStatus>;
  let api: {
    submit: ReturnType<typeof vi.fn<() => Observable<SubmitDocumentResponse>>>;
    list: ReturnType<typeof vi.fn<() => Observable<readonly DocumentListItem[]>>>;
    get: ReturnType<typeof vi.fn<() => Observable<DocumentDetails>>>;
  };

  beforeEach(() => {
    statuses = new Subject<DocumentStatus>();
    api = {
      submit: vi.fn(() => of<SubmitDocumentResponse>({ id: 'doc-1', status: 'Received' })),
      list: vi.fn(() => of<readonly DocumentListItem[]>([])),
      get: vi.fn(() => of(details('Draft'))),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: DocumentsApiService, useValue: api },
        { provide: DocumentEventsService, useValue: { watch: () => statuses.asObservable() } },
      ],
    });
  });

  async function render<T>(component: Type<T>, inputs: Record<string, unknown> = {}) {
    const fixture: ComponentFixture<T> = TestBed.createComponent(component);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    await fixture.whenStable();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  async function submitText(
    fixture: ComponentFixture<unknown>,
    element: HTMLElement,
  ): Promise<void> {
    const textarea = element.querySelector('textarea')!;
    textarea.value = 'Texto de la convocatoria que hay que adaptar.';
    textarea.dispatchEvent(new Event('input'));
    element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  describe('NewDocumentPage', () => {
    it('shows the progress and opens the document view when it is ready', async () => {
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
      const { fixture, element } = await render(NewDocumentPage);

      await submitText(fixture, element);
      expect(element.querySelector('app-progress-steps')).not.toBeNull();
      expect(element.querySelector('form')).toBeNull();

      statuses.next('Processing');
      statuses.next('Draft');
      statuses.complete();
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith(['/documentos', 'doc-1']);
    });

    it('shows a Spanish message and a retry button when the API returns a ProblemDetails', async () => {
      api.submit.mockReturnValueOnce(
        throwError(() => new ApiError(ERROR_TEXTS.tooManyRequests, 429, null)),
      );
      const { fixture, element } = await render(NewDocumentPage);

      await submitText(fixture, element);

      const alert = element.querySelector('.error[role="alert"]');
      expect(alert?.textContent).toContain(ERROR_TEXTS.tooManyRequests);
      expect(element.querySelector('form')).not.toBeNull();

      Array.from(alert!.querySelectorAll('button'))
        .find((button) => button.textContent?.includes(PROGRESS_TEXTS.retry))!
        .click();
      await fixture.whenStable();

      expect(api.submit).toHaveBeenCalledTimes(2);
      expect(element.querySelector('app-progress-steps')).not.toBeNull();
    });

    it('marks the failed step and offers to retry when the adaptation fails', async () => {
      const { fixture, element } = await render(NewDocumentPage);
      await submitText(fixture, element);

      statuses.next('Error');
      statuses.complete();
      await fixture.whenStable();

      expect(element.textContent).toContain(PROGRESS_TEXTS.adaptationFailed);
      expect(element.querySelector('.step--failed')?.textContent).toContain(
        'Adaptando (con error)',
      );
      expect(element.textContent).toContain(PROGRESS_TEXTS.startOver);
    });
  });

  describe('DocumentsPage', () => {
    it('loads the list of documents', async () => {
      api.list.mockReturnValue(
        of([
          {
            id: 'doc-1',
            title: 'Bases',
            status: 'Draft',
            createdAt: '2026-11-16T10:00:00Z',
            updatedAt: '2026-11-16T10:00:00Z',
            provider: 'Servidor local',
          },
        ]),
      );

      const { element } = await render(DocumentsPage);

      expect(element.querySelector('h1')?.textContent).toBe('Mis documentos');
      expect(element.querySelector('td')?.textContent).toContain('Bases');
    });

    it('shows the error and loads again on retry', async () => {
      api.list.mockReturnValueOnce(throwError(() => new ApiError(ERROR_TEXTS.offline, 0, null)));
      const { fixture, element } = await render(DocumentsPage);

      expect(element.querySelector('[role="alert"]')?.textContent).toContain(ERROR_TEXTS.offline);
      element.querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
      await fixture.whenStable();

      expect(api.list).toHaveBeenCalledTimes(2);
      expect(element.textContent).toContain('Todavía no tienes documentos.');
    });
  });

  describe('DocumentPage', () => {
    it('shows the title, the status and the side-by-side review of a draft', async () => {
      const { element } = await render(DocumentPage, { id: 'doc-1' });

      expect(api.get).toHaveBeenCalledWith('doc-1');
      expect(element.querySelector('h1')?.textContent).toBe('Bases de la convocatoria');
      expect(element.textContent).toContain(REVIEW_TEXTS.draftLabel);
      expect(element.querySelector('app-document-review')).not.toBeNull();
      expect(element.querySelector('app-progress-steps')).toBeNull();
    });

    it('does not show the review while the document has no adaptation', async () => {
      api.get.mockReturnValueOnce(of(details('Processing')));

      const { element } = await render(DocumentPage, { id: 'doc-1' });

      expect(element.querySelector('app-document-review')).toBeNull();
    });

    it('shows the progress while the document is being adapted', async () => {
      api.get.mockReturnValueOnce(of(details('Processing')));

      const { element } = await render(DocumentPage, { id: 'doc-1' });

      expect(element.querySelector('app-progress-steps')).not.toBeNull();
    });

    it('shows a not-found message', async () => {
      api.get.mockReturnValue(
        throwError(() => new ApiError(ERROR_TEXTS.notFound, 404, 'document.not_found')),
      );

      const { element } = await render(DocumentPage, { id: 'doc-1' });

      expect(element.querySelector('[role="alert"]')?.textContent).toContain(ERROR_TEXTS.notFound);
    });
  });
});
