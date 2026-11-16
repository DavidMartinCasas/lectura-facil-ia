// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { NEVER, Observable, Subject, of, throwError } from 'rxjs';
import { adaptationFixture, documentFixture } from '../../testing/document-fixtures';
import { ApiError } from '../errors/api-error';
import { ERROR_TEXTS } from '../ui-texts';
import { DocumentEventsService } from './document-events.service';
import { DocumentViewStore } from './document-view.store';
import { DocumentDetails, ExportFormat } from './document.models';
import { DocumentsApiService, DownloadedFile } from './documents-api.service';
import { FileDownloadService } from './file-download.service';

const file: DownloadedFile = { blob: new Blob(['x']), fileName: 'ayudas.docx' };

describe('DocumentViewStore (exportación)', () => {
  let exportDocument: ReturnType<
    typeof vi.fn<(id: string, format: ExportFormat) => Observable<DownloadedFile>>
  >;
  let save: ReturnType<typeof vi.fn<(blob: Blob, fileName: string) => void>>;
  let saveEdit: ReturnType<typeof vi.fn<() => Observable<DocumentDetails>>>;
  let store: DocumentViewStore;

  function setUp(document: DocumentDetails) {
    exportDocument = vi.fn(() => of(file));
    saveEdit = vi.fn(() => new Subject<DocumentDetails>());
    save = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        DocumentViewStore,
        {
          provide: DocumentsApiService,
          useValue: { get: () => of(document), exportDocument, saveEdit },
        },
        { provide: DocumentEventsService, useValue: { watch: () => NEVER } },
        { provide: FileDownloadService, useValue: { save } },
      ],
    });
    store = TestBed.inject(DocumentViewStore);
    store.load('doc-1');
  }

  const exportable = () =>
    documentFixture('Draft', {
      adaptation: adaptationFixture({ pendingKeyFactCount: 0, canExport: true }),
    });

  it('downloads the file that the server generates', () => {
    setUp(exportable());

    store.exportAdaptation('pdf');

    expect(exportDocument).toHaveBeenCalledWith('doc-1', 'pdf');
    expect(save).toHaveBeenCalledWith(file.blob, 'ayudas.docx');
    expect(store.exporting()).toBe(false);
    expect(store.lastAction()).toBe('exported');
  });

  it('does not ask the server while there are pending warnings', () => {
    setUp(documentFixture('Draft'));

    store.exportAdaptation('docx');

    expect(exportDocument).not.toHaveBeenCalled();
  });

  it('ignores a second export while one is in progress', () => {
    setUp(exportable());
    exportDocument.mockReturnValueOnce(new Subject<DownloadedFile>());

    store.exportAdaptation('docx');
    store.exportAdaptation('txt');

    expect(store.exporting()).toBe(true);
    expect(exportDocument).toHaveBeenCalledTimes(1);
  });

  it('waits until a pending edit is saved before exporting', () => {
    setUp(exportable());
    store.saveEdit({ title: 'Ayudas', sections: [], glossary: [] });

    store.exportAdaptation('docx');

    expect(store.saving()).toBe(true);
    expect(exportDocument).not.toHaveBeenCalled();
  });

  it('shows the message of the server when the export is refused', () => {
    setUp(exportable());
    exportDocument.mockReturnValueOnce(
      throwError(
        () => new ApiError(ERROR_TEXTS.exportPendingWarnings, 409, 'export.pending_warnings'),
      ),
    );

    store.exportAdaptation('docx');

    expect(store.actionError()).toBe('Revisa los avisos antes de exportar');
    expect(store.exporting()).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
