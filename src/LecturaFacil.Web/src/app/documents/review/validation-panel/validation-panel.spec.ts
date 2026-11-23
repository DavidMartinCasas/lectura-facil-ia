// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import {
  DocumentStatus,
  ReviewableDocument,
  ValidationRecord,
  ValidationResult,
} from '../../../core/documents/document.models';
import { VALIDATION_TEXTS } from '../../../core/ui-texts';
import { adaptationFixture, documentFixture } from '../../../testing/document-fixtures';
import { ValidationPanelComponent } from './validation-panel';

const VALIDATION: ValidationRecord = {
  version: 1,
  outcome: 'Approved',
  validatedBy: 'Grupo de lectura',
  notes: 'Se entiende bien.',
  recordedAt: '2026-11-23T10:00:00Z',
};

function reviewable(status: DocumentStatus, overrides: Partial<ReviewableDocument> = {}) {
  return {
    ...documentFixture(status, { adaptation: adaptationFixture() }),
    ...overrides,
  } as ReviewableDocument;
}

async function render(document: ReviewableDocument, saving = false) {
  const fixture = TestBed.createComponent(ValidationPanelComponent);
  fixture.componentRef.setInput('document', document);
  fixture.componentRef.setInput('saving', saving);
  const events = { send: 0, withdraw: 0, publish: 0, records: [] as ValidationResult[] };
  fixture.componentInstance.send.subscribe(() => events.send++);
  fixture.componentInstance.withdraw.subscribe(() => events.withdraw++);
  fixture.componentInstance.publish.subscribe(() => events.publish++);
  fixture.componentInstance.record.subscribe((result) => events.records.push(result));
  await fixture.whenStable();
  const element = fixture.nativeElement as HTMLElement;
  const button = (text: string) =>
    [...element.querySelectorAll<HTMLButtonElement>('button')].find((candidate) =>
      candidate.textContent?.includes(text),
    )!;
  return { fixture, element, events, button };
}

function type(element: HTMLElement, selector: string, value: string): void {
  const field = element.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
  field.value = value;
  field.dispatchEvent(new Event('input'));
}

describe('ValidationPanelComponent', () => {
  it('offers to send a reviewed draft to validation', async () => {
    const { element, events, button } = await render(
      reviewable('Draft', { canSendToValidation: true }),
    );

    button(VALIDATION_TEXTS.send).click();

    expect(events.send).toBe(1);
    expect(element.textContent).not.toContain(VALIDATION_TEXTS.sendBlocked);
  });

  it('says why a draft with pending key facts cannot be sent yet', async () => {
    const { element, events, button } = await render(
      reviewable('Draft', { canSendToValidation: false }),
    );

    const send = button(VALIDATION_TEXTS.send);
    expect(send.disabled).toBe(true);
    expect(send.getAttribute('aria-describedby')).toBe('send-blocked');
    expect(element.querySelector('#send-blocked')?.textContent).toContain(
      VALIDATION_TEXTS.sendBlocked,
    );
    expect(events.send).toBe(0);
  });

  it('reminds a draft of the changes the group asked for', async () => {
    const { element } = await render(
      reviewable('Draft', {
        validation: { ...VALIDATION, outcome: 'ChangesRequested', notes: 'Falta el plazo.' },
      }),
    );

    const note = element.querySelector('.previous')!;
    expect(note.textContent).toContain(
      VALIDATION_TEXTS.changesRequested('23 de noviembre de 2026', 'Grupo de lectura'),
    );
    expect(note.textContent).toContain('Falta el plazo.');
  });

  it('asks for the outcome and who validated it before recording the result', async () => {
    const { fixture, element, events, button } = await render(reviewable('PendingValidation'));

    button(VALIDATION_TEXTS.record).click();
    await fixture.whenStable();

    expect(events.records).toEqual([]);
    expect(element.querySelector('#outcome-error')?.textContent).toContain(
      VALIDATION_TEXTS.outcomeRequired,
    );
    expect(element.textContent).toContain(VALIDATION_TEXTS.validatedByRequired);
  });

  it('records what the group decided, without empty notes', async () => {
    const { fixture, element, events, button } = await render(reviewable('PendingValidation'));

    element.querySelectorAll<HTMLInputElement>('input[type="radio"]')[1].click();
    type(element, 'input[name="validatedBy"]', '  Grupo de lectura  ');
    type(element, 'textarea[name="notes"]', '   ');
    await fixture.whenStable();
    button(VALIDATION_TEXTS.record).click();

    expect(events.records).toEqual([
      { outcome: 'ChangesRequested', validatedBy: 'Grupo de lectura', notes: null },
    ]);
  });

  it('can withdraw a document from validation', async () => {
    const { events, button } = await render(reviewable('PendingValidation'));

    button(VALIDATION_TEXTS.withdraw).click();

    expect(events.withdraw).toBe(1);
  });

  it('shows who validated it and offers to publish it', async () => {
    const { element, events, button } = await render(
      reviewable('Validated', { validation: VALIDATION }),
    );

    expect(element.textContent).toContain(
      VALIDATION_TEXTS.validated('23 de noviembre de 2026', 'Grupo de lectura'),
    );
    expect(element.textContent).toContain('Se entiende bien.');
    button(VALIDATION_TEXTS.publish).click();

    expect(events.publish).toBe(1);
  });

  it('says a published document is the final version and offers no more steps', async () => {
    const { element } = await render(reviewable('Published', { validation: VALIDATION }));

    expect(element.textContent).toContain(VALIDATION_TEXTS.published);
    expect(element.querySelector('button')).toBeNull();
  });
});
