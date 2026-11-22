// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { DocumentsApiService, fileNameFrom } from './documents-api.service';

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

  it('sends the anti-bot token in its header, also with a PDF', async () => {
    const file = new File(['%PDF-1.7'], 'bases.pdf', { type: 'application/pdf' });
    const text = firstValueFrom(service.submit({ kind: 'text', text: 'Texto', humanToken: 't1' }));
    const pdf = firstValueFrom(service.submit({ kind: 'pdf', file, humanToken: 't2' }));

    const requests = controller.match('/api/documents');
    expect(requests.map((request) => request.request.headers.get('X-Turnstile-Token'))).toEqual([
      't1',
      't2',
    ]);
    requests.forEach((request) => request.flush({ id: 'abc', status: 'Received' }));
    await Promise.all([text, pdf]);
  });

  it('sends no anti-bot header without a token', async () => {
    const response = firstValueFrom(service.submit({ kind: 'text', text: 'Texto' }));

    const request = controller.expectOne('/api/documents');
    expect(request.request.headers.has('X-Turnstile-Token')).toBe(false);
    request.flush({ id: 'abc', status: 'Received' });
    await response;
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

  it('lists the first page of documents without filters', async () => {
    const response = firstValueFrom(service.list());

    const request = controller.expectOne('/api/documents');
    expect(request.request.method).toBe('GET');
    request.flush({ items: [], nextCursor: null });
    expect(await response).toEqual({ items: [], nextCursor: null });
  });

  it('sends the title, each status and the cursor as query parameters', async () => {
    const response = firstValueFrom(
      service.list({ title: '  bases de ayudas ', statuses: ['Draft', 'Error'] }, 'abc'),
    );

    const request = controller.expectOne(
      (req) => req.url === '/api/documents' && req.method === 'GET',
    );
    expect(request.request.params.get('title')).toBe('bases de ayudas');
    expect(request.request.params.getAll('status')).toEqual(['Draft', 'Error']);
    expect(request.request.params.get('cursor')).toBe('abc');
    request.flush({ items: [], nextCursor: null });
    await response;
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

  it('exports with the chosen format and keeps the file name of the server', async () => {
    const response = firstValueFrom(service.exportDocument('a/b', 'docx'));

    const request = controller.expectOne('/api/documents/a%2Fb/export?format=docx');
    expect(request.request.method).toBe('POST');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['PK']), {
      headers: {
        'Content-Disposition': "attachment; filename=ayudas.docx; filename*=UTF-8''ayudas.docx",
      },
    });
    const file = await response;
    expect(file.fileName).toBe('ayudas.docx');
    expect(await file.blob.text()).toBe('PK');
  });

  it('uses a generic name when the server does not send a safe one', async () => {
    const response = firstValueFrom(service.exportDocument('abc', 'md'));

    controller.expectOne('/api/documents/abc/export?format=md').flush(new Blob(['#']), {
      headers: { 'Content-Disposition': 'attachment; filename="../x.md"' },
    });

    expect((await response).fileName).toBe('documento.md');
  });
});

describe('fileNameFrom', () => {
  it.each([
    ['attachment; filename=ayudas.pdf', 'ayudas.pdf'],
    ['attachment; filename="ayudas.txt"', 'ayudas.txt'],
    ['attachment; filename=../../etc/passwd', null],
    ['attachment; filename=".hidden"', null],
    [null, null],
  ])('reads %s as %s', (header, expected) => {
    expect(fileNameFrom(header)).toBe(expected);
  });
});
