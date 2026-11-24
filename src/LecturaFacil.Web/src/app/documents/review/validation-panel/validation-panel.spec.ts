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
  round: 1,
  version: 1,
  outcome: 'Approved',
  validatedBy: 'Grupo de lectura',
  participants: 5,
  hardSections: [],
  hardGlossary: false,
  notes: 'Se entiende bien.',
  recordedAt: '2026-11-23T10:00:00Z',
};

const CHANGES: ValidationRecord = {
  ...VALIDATION,
  outcome: 'ChangesRequested',
  participants: 4,
  hardSections: ['¿Cuánto dinero dan?'],
  hardGlossary: true,
  notes: 'Falta el plazo.',
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

function checkboxes(element: HTMLElement): HTMLInputElement[] {
  return [...element.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')];
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

  it('reminds a draft of the changes the group asked for and of the parts that were hard', async () => {
    const { element } = await render(
      reviewable('Draft', { validation: CHANGES, validations: [CHANGES] }),
    );

    const note = element.querySelector('.previous')!;
    expect(note.textContent).toContain(
      VALIDATION_TEXTS.changesRequested('23 de noviembre de 2026', 'Grupo de lectura (4 personas)'),
    );
    expect(note.textContent).toContain('Falta el plazo.');
    expect([...note.querySelectorAll('.hard-list li')].map((item) => item.textContent)).toEqual([
      '¿Cuánto dinero dan?',
      VALIDATION_TEXTS.glossaryPart,
    ]);
  });

  it('asks to change the text before sending again the same version the group did not understand', async () => {
    const { element, button } = await render(
      reviewable('Draft', { canSendToValidation: false, validation: CHANGES }),
    );

    expect(button(VALIDATION_TEXTS.send).disabled).toBe(true);
    expect(element.querySelector('#send-blocked')?.textContent).toContain(
      VALIDATION_TEXTS.sendUnchanged,
    );
  });

  it('asks for the outcome, who validated it and how many people before recording the result', async () => {
    const { fixture, element, events, button } = await render(reviewable('PendingValidation'));

    button(VALIDATION_TEXTS.record).click();
    await fixture.whenStable();

    expect(events.records).toEqual([]);
    expect(element.querySelector('#outcome-error')?.textContent).toContain(
      VALIDATION_TEXTS.outcomeRequired,
    );
    expect(element.textContent).toContain(VALIDATION_TEXTS.validatedByRequired);
    expect(element.querySelector('#participants-error')?.textContent).toContain(
      VALIDATION_TEXTS.participantsRequired,
    );
  });

  it('does not accept an impossible number of people', async () => {
    const { fixture, element, events, button } = await render(reviewable('PendingValidation'));

    element.querySelectorAll<HTMLInputElement>('input[type="radio"]')[0].click();
    type(element, 'input[name="validatedBy"]', 'Grupo');
    type(element, 'input[name="participants"]', '100');
    await fixture.whenStable();
    button(VALIDATION_TEXTS.record).click();
    await fixture.whenStable();

    expect(events.records).toEqual([]);
    expect(element.querySelector('#participants-error')).not.toBeNull();
  });

  it('records an approval without hard parts or empty notes', async () => {
    const { fixture, element, events, button } = await render(reviewable('PendingValidation'));

    element.querySelectorAll<HTMLInputElement>('input[type="radio"]')[0].click();
    await fixture.whenStable();
    expect(checkboxes(element)).toEqual([]);
    type(element, 'input[name="validatedBy"]', '  Grupo de lectura  ');
    type(element, 'input[name="participants"]', '5');
    type(element, 'textarea[name="notes"]', '   ');
    await fixture.whenStable();
    button(VALIDATION_TEXTS.record).click();

    expect(events.records).toEqual([
      {
        outcome: 'Approved',
        validatedBy: 'Grupo de lectura',
        participants: 5,
        hardSections: [],
        hardGlossary: false,
        notes: null,
      },
    ]);
  });

  it('marks the sections and the glossary that were hard when the group asks for changes', async () => {
    const { fixture, element, events, button } = await render(reviewable('PendingValidation'));

    element.querySelectorAll<HTMLInputElement>('input[type="radio"]')[1].click();
    await fixture.whenStable();
    const [section, glossary] = checkboxes(element);
    expect(element.querySelector('.hard-parts')?.textContent).toContain('¿Cuánto dinero dan?');
    section.click();
    glossary.click();
    type(element, 'input[name="validatedBy"]', 'Grupo');
    type(element, 'input[name="participants"]', '4');
    await fixture.whenStable();
    button(VALIDATION_TEXTS.record).click();

    expect(events.records).toEqual([
      {
        outcome: 'ChangesRequested',
        validatedBy: 'Grupo',
        participants: 4,
        hardSections: [0],
        hardGlossary: true,
        notes: null,
      },
    ]);
  });

  it('can withdraw a document from validation', async () => {
    const { events, button } = await render(reviewable('PendingValidation'));

    button(VALIDATION_TEXTS.withdraw).click();

    expect(events.withdraw).toBe(1);
  });

  it('shows who validated it, how many people and offers to publish it', async () => {
    const { element, events, button } = await render(
      reviewable('Validated', { validation: VALIDATION, validations: [VALIDATION] }),
    );

    expect(element.textContent).toContain(
      VALIDATION_TEXTS.validated('23 de noviembre de 2026', 'Grupo de lectura (5 personas)'),
    );
    expect(element.textContent).toContain('Se entiende bien.');
    expect(element.querySelector('.rounds')).toBeNull();
    button(VALIDATION_TEXTS.publish).click();

    expect(events.publish).toBe(1);
  });

  it('shows every round when there was more than one', async () => {
    const approved = { ...VALIDATION, round: 2, version: 2, recordedAt: '2026-11-26T10:00:00Z' };
    const { element } = await render(
      reviewable('Validated', { validation: approved, validations: [CHANGES, approved] }),
    );

    const rounds = [...element.querySelectorAll('.rounds li')].map((item) =>
      item.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(rounds).toEqual([
      `${VALIDATION_TEXTS.roundOutcomes.ChangesRequested} ${VALIDATION_TEXTS.roundLine(1, 1, '23 de noviembre de 2026', 'Grupo de lectura (4 personas)', 2)}`,
      `${VALIDATION_TEXTS.roundOutcomes.Approved} ${VALIDATION_TEXTS.roundLine(2, 2, '26 de noviembre de 2026', 'Grupo de lectura (5 personas)', 0)}`,
    ]);
  });

  it('says a published document is the final version and offers no more steps', async () => {
    const { element } = await render(
      reviewable('Published', { validation: VALIDATION, validations: [VALIDATION] }),
    );

    expect(element.textContent).toContain(VALIDATION_TEXTS.published);
    expect(element.querySelector('button')).toBeNull();
  });
});
