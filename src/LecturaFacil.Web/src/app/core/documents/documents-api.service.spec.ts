// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { DocumentsApiService } from './documents-api.service';

describe('DocumentsApiService', () => {
  let service: DocumentsApiService;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DocumentsApiService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('sends a pasted text as JSON with its title', async () => {
    const response = firstValueFrom(
      service.submit({ kind: 'text', text: 'Texto', title: '  Bases  ' }),
    );

    const request = controller.expectOne('/api/documents');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ text: 'Texto', title: 'Bases' });
    request.flush({ id: 'abc', status: 'Received' });
    expect(await response).toEqual({ id: 'abc', status: 'Received' });
  });

  it('sends an empty title as null', async () => {
    const response = firstValueFrom(service.submit({ kind: 'text', text: 'Texto', title: '   ' }));

    const request = controller.expectOne('/api/documents');
    expect(request.request.body).toEqual({ text: 'Texto', title: null });
    request.flush({ id: 'abc', status: 'Received' });
    await response;
  });

  it('sends a PDF as multipart/form-data in the "file" field', async () => {
    const file = new File(['%PDF-1.7'], 'bases.pdf', { type: 'application/pdf' });
    const response = firstValueFrom(service.submit({ kind: 'pdf', file }));

    const request = controller.expectOne('/api/documents');
    const body = request.request.body as FormData;
    expect(body).toBeInstanceOf(FormData);
    expect((body.get('file') as File).name).toBe('bases.pdf');
    request.flush({ id: 'abc', status: 'Received' });
    await response;
  });

  it('lists the documents of the user', async () => {
    const response = firstValueFrom(service.list());

    const request = controller.expectOne('/api/documents');
    expect(request.request.method).toBe('GET');
    request.flush([]);
    expect(await response).toEqual([]);
  });

  it('gets a document by its id, encoding it in the URL', async () => {
    const response = firstValueFrom(service.get('a/b'));

    controller.expectOne('/api/documents/a%2Fb').flush({ id: 'a/b' });
    expect(await response).toEqual({ id: 'a/b' });
  });

  it('saves an edit of the latest version with PUT', async () => {
    const edit = {
      title: 'Ayudas',
      sections: [{ question: '¿Qué?', sentences: ['Una.'] }],
      glossary: [],
    };
    const response = firstValueFrom(service.saveEdit('doc-1', 2, edit));

    const request = controller.expectOne('/api/documents/doc-1/versions/2');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(edit);
    request.flush({ id: 'doc-1' });
    expect(await response).toEqual({ id: 'doc-1' });
  });

  it('acknowledges a key fact of a version with POST', async () => {
    const response = firstValueFrom(service.acknowledgeKeyFact('doc-1', 2, 3));

    const request = controller.expectOne('/api/documents/doc-1/key-facts/3/acknowledge');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ version: 2 });
    request.flush({ id: 'doc-1' });
    await response;
  });
});
