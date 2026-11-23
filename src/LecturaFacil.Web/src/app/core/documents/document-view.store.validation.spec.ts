// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { NEVER, of, throwError } from 'rxjs';
import { adaptationFixture, documentFixture } from '../../testing/document-fixtures';
import { ApiError } from '../errors/api-error';
import { DocumentEventsService } from './document-events.service';
import { DocumentViewStore } from './document-view.store';
import { DocumentDetails, DocumentStatus } from './document.models';
import { DocumentsApiService } from './documents-api.service';

function withAdaptation(status: DocumentStatus, overrides: Partial<DocumentDetails> = {}) {
  return documentFixture(status, { adaptation: adaptationFixture(), ...overrides });
}

describe('DocumentViewStore (validación)', () => {
  function setUp(current: DocumentDetails) {
    const api = {
      get: vi.fn(() => of(current)),
      sendToValidation: vi.fn(() => of(withAdaptation('PendingValidation'))),
      recordValidation: vi.fn(() => of(withAdaptation('Validated'))),
      withdrawFromValidation: vi.fn(() => of(withAdaptation('Draft'))),
      publish: vi.fn(() => of(withAdaptation('Published'))),
    };
    TestBed.configureTestingModule({
      providers: [
        DocumentViewStore,
        { provide: DocumentsApiService, useValue: api },
        { provide: DocumentEventsService, useValue: { watch: () => NEVER } },
      ],
    });
    const store = TestBed.inject(DocumentViewStore);
    store.load('doc-1');
    return { api, store };
  }

  it('sends the version on screen to validation', () => {
    const { api, store } = setUp(withAdaptation('Draft', { canSendToValidation: true }));

    store.sendToValidation();

    expect(api.sendToValidation).toHaveBeenCalledWith('doc-1', 1);
    expect(store.status()).toBe('PendingValidation');
    expect(store.lastAction()).toBe('sent');
  });

  it('does not send a draft that cannot be sent yet', () => {
    const { api, store } = setUp(withAdaptation('Draft', { canSendToValidation: false }));

    store.sendToValidation();

    expect(api.sendToValidation).not.toHaveBeenCalled();
  });

  it('records the result and announces whether it was validated or needs changes', () => {
    const { api, store } = setUp(withAdaptation('PendingValidation'));

    store.recordValidation({ outcome: 'Approved', validatedBy: 'Grupo', notes: null });
    expect(api.recordValidation).toHaveBeenCalledWith('doc-1', 1, {
      outcome: 'Approved',
      validatedBy: 'Grupo',
      notes: null,
    });
    expect(store.lastAction()).toBe('approved');
  });

  it('announces that a document needs changes', () => {
    const { store } = setUp(withAdaptation('PendingValidation'));

    store.recordValidation({ outcome: 'ChangesRequested', validatedBy: 'Grupo', notes: 'Plazo' });

    expect(store.lastAction()).toBe('changesRequested');
  });

  it('withdraws a pending document and publishes only a validated one', () => {
    const pending = setUp(withAdaptation('PendingValidation'));
    pending.store.publish();
    pending.store.withdrawFromValidation();

    expect(pending.api.publish).not.toHaveBeenCalled();
    expect(pending.api.withdrawFromValidation).toHaveBeenCalledWith('doc-1');
    expect(pending.store.status()).toBe('Draft');
    expect(pending.store.lastAction()).toBe('withdrawn');
  });

  it('publishes a validated document', () => {
    const { api, store } = setUp(withAdaptation('Validated'));

    store.publish();

    expect(api.publish).toHaveBeenCalledWith('doc-1');
    expect(store.status()).toBe('Published');
    expect(store.lastAction()).toBe('published');
  });

  it('shows the message of the server when a step fails', () => {
    const { api, store } = setUp(withAdaptation('Validated'));
    api.publish.mockReturnValueOnce(
      throwError(
        () =>
          new ApiError(
            'Solo se puede publicar un documento validado.',
            409,
            'validation.not_validated',
          ),
      ),
    );

    store.publish();

    expect(store.actionError()).toBe('Solo se puede publicar un documento validado.');
    expect(store.saving()).toBe(false);
  });
});
