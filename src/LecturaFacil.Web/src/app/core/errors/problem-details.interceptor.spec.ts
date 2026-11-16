// SPDX-License-Identifier: Apache-2.0
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ERROR_TEXTS } from '../ui-texts';
import { ApiError } from './api-error';
import { problemDetailsInterceptor } from './problem-details.interceptor';

describe('problemDetailsInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  async function failWith(status: number, body: object): Promise<ApiError> {
    const request = firstValueFrom(http.get('/api/documents'));
    controller.expectOne('/api/documents').flush(body, {
      status,
      statusText: 'Error',
      headers: { 'Content-Type': 'application/problem+json' },
    });
    try {
      await request;
    } catch (error) {
      return error as ApiError;
    }
    throw new Error('The request should have failed.');
  }

  it('translates a known problem code into a Spanish message', async () => {
    const error = await failWith(422, {
      status: 422,
      detail: 'Este PDF parece escaneado.',
      code: 'document.scanned_pdf',
    });

    expect(error).toBeInstanceOf(ApiError);
    expect(error.message).toBe(ERROR_TEXTS.scannedPdf);
    expect(error.status).toBe(422);
    expect(error.code).toBe('document.scanned_pdf');
  });

  it('never shows the technical details that come in the response', async () => {
    const error = await failWith(500, {
      status: 500,
      title: 'System.InvalidOperationException',
      detail: 'Npgsql: connection refused at 10.0.0.5:5432',
      trace: 'at LecturaFacil.Infrastructure…',
    });

    expect(error.message).toBe(ERROR_TEXTS.generic);
    expect(error.message).not.toContain('Npgsql');
    expect(error.code).toBeNull();
  });

  it.each([
    [400, ERROR_TEXTS.invalidRequest],
    [404, ERROR_TEXTS.notFound],
    [413, ERROR_TEXTS.fileTooLarge],
    [415, ERROR_TEXTS.unsupported],
    [422, ERROR_TEXTS.unprocessable],
    [429, ERROR_TEXTS.tooManyRequests],
    [503, ERROR_TEXTS.unavailable],
  ])('uses a message by status %i when the code is unknown', async (status, message) => {
    const error = await failWith(status, { status, code: 'algo.desconocido' });

    expect(error.message).toBe(message);
  });

  it('reports that the server cannot be reached when there is no response', async () => {
    const request = firstValueFrom(http.get('/api/documents'));
    controller.expectOne('/api/documents').error(new ProgressEvent('error'), { status: 0 });

    await expect(request).rejects.toMatchObject({ message: ERROR_TEXTS.offline, status: 0 });
  });

  it('reads the problem code of a failed download', async () => {
    const request = firstValueFrom(
      http.post('/api/documents/abc/export', null, { responseType: 'blob' }),
    );
    controller.expectOne('/api/documents/abc/export').flush(
      new Blob([JSON.stringify({ status: 409, code: 'export.pending_warnings' })], {
        type: 'application/problem+json',
      }),
      { status: 409, statusText: 'Conflict' },
    );

    const error = await request.catch((caught: unknown) => caught as ApiError);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).message).toBe(ERROR_TEXTS.exportPendingWarnings);
    expect((error as ApiError).code).toBe('export.pending_warnings');
  });

  it('falls back to the status message when a failed download is not JSON', async () => {
    const request = firstValueFrom(
      http.post('/api/documents/abc/export', null, { responseType: 'blob' }),
    );
    controller
      .expectOne('/api/documents/abc/export')
      .flush(new Blob(['<html>']), { status: 429, statusText: 'Too Many Requests' });

    const error = (await request.catch((caught: unknown) => caught)) as ApiError;

    expect(error.message).toBe(ERROR_TEXTS.tooManyRequests);
  });
});
