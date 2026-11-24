// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import {
  DocumentStatus,
  ReviewableDocument,
  ValidationRecord,
  ValidationResult,
} from '../../../core/documents/document.models';
import { VALIDATION_TEXTS } from '../../../core/ui-texts';
import { adaptationFixture, documentFixture } from '../../../testing/document-fixtures';
import { ValidationPanelComponent } from './validation-panel';
import { ValidationResultDialogComponent } from './validation-result-dialog';

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

const RESULT: ValidationResult = {
  outcome: 'Approved',
  validatedBy: 'Grupo',
  participants: 4,
  hardSections: [],
  hardGlossary: false,
  notes: null,
};

function reviewable(status: DocumentStatus, overrides: Partial<ReviewableDocument> = {}) {
  return {
    ...documentFixture(status, { adaptation: adaptationFixture() }),
    ...overrides,
  } as ReviewableDocument;
}

async function render(
  document: ReviewableDocument,
  { saving = false, dialogResult }: { saving?: boolean; dialogResult?: ValidationResult } = {},
) {
  const dialog = { open: vi.fn(() => ({ afterClosed: () => of(dialogResult) })) };
  TestBed.configureTestingModule({ providers: [{ provide: MatDialog, useValue: dialog }] });
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
  const openDetails = async () => {
    button(VALIDATION_TEXTS.showDetails).click();
    await fixture.whenStable();
  };
  return { fixture, element, events, button, dialog, openDetails };
}

function text(element: Element | null | undefined): string {
  return element?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

describe('ValidationPanelComponent', () => {
  it('shows the four steps with the current one marked', async () => {
    const { element } = await render(reviewable('Validated', { validation: VALIDATION }));

    const steps = [...element.querySelectorAll('.steps li')];
    expect(steps.map(text)).toEqual([
      `✓${VALIDATION_TEXTS.stepDone} ${VALIDATION_TEXTS.steps['Draft']}`,
      `✓${VALIDATION_TEXTS.stepDone} ${VALIDATION_TEXTS.steps['PendingValidation']}`,
      VALIDATION_TEXTS.steps['Validated'],
      VALIDATION_TEXTS.steps['Published'],
    ]);
    expect(steps[2].getAttribute('aria-current')).toBe('step');
    expect(element.querySelectorAll('[aria-current]')).toHaveLength(1);
  });

  it('offers to send a reviewed draft to validation', async () => {
    const { element, events, button } = await render(
      reviewable('Draft', { canSendToValidation: true }),
    );

    button(VALIDATION_TEXTS.send).click();

    expect(events.send).toBe(1);
    expect(element.querySelector('#send-blocked')).toBeNull();
    expect(text(element.querySelector('.summary'))).toContain(VALIDATION_TEXTS.draftHint);
  });

  it('says why a draft with pending key facts cannot be sent yet', async () => {
    const { element, events, button } = await render(
      reviewable('Draft', { canSendToValidation: false }),
    );

    const send = button(VALIDATION_TEXTS.send);
    expect(send.disabled).toBe(true);
    expect(send.getAttribute('aria-describedby')).toBe('send-blocked');
    expect(text(element.querySelector('#send-blocked'))).toBe(VALIDATION_TEXTS.sendBlocked);
    expect(events.send).toBe(0);
  });

  it('reminds a draft of the changes the group asked for, with the hard parts in the details', async () => {
    const { element, openDetails } = await render(
      reviewable('Draft', { validation: CHANGES, validations: [CHANGES] }),
    );

    const summary = text(element.querySelector('.summary'));
    expect(summary).toContain(
      VALIDATION_TEXTS.changesRequested('23 de noviembre de 2026', 'Grupo de lectura (4 personas)'),
    );
    expect(summary).toContain(VALIDATION_TEXTS.changesMarked);
    expect(element.querySelector('#validation-details')).toBeNull();

    await openDetails();

    const details = element.querySelector('#validation-details')!;
    expect([...details.querySelectorAll('.hard-list li')].map(text)).toEqual([
      '¿Cuánto dinero dan?',
      VALIDATION_TEXTS.glossaryPart,
    ]);
    expect(text(details)).toContain('Falta el plazo.');
    expect(element.querySelector('.toggle')?.getAttribute('aria-expanded')).toBe('true');
  });

  it('asks to change the text before sending again the same version the group did not understand', async () => {
    const { element, button } = await render(
      reviewable('Draft', { canSendToValidation: false, validation: CHANGES }),
    );

    expect(button(VALIDATION_TEXTS.send).disabled).toBe(true);
    expect(text(element.querySelector('#send-blocked'))).toBe(VALIDATION_TEXTS.sendUnchanged);
  });

  it('opens the form with the sections of the version and records what it returns', async () => {
    const { events, button, dialog } = await render(reviewable('PendingValidation'), {
      dialogResult: RESULT,
    });

    button(VALIDATION_TEXTS.openRecord).click();

    expect(dialog.open).toHaveBeenCalledWith(
      ValidationResultDialogComponent,
      expect.objectContaining({
        data: { sections: ['¿Cuánto dinero dan?'], hasGlossary: true },
        restoreFocus: true,
      }),
    );
    expect(events.records).toEqual([RESULT]);
  });

  it('records nothing when the form is cancelled', async () => {
    const { events, button } = await render(reviewable('PendingValidation'));

    button(VALIDATION_TEXTS.openRecord).click();

    expect(events.records).toEqual([]);
  });

  it('can withdraw a document from validation', async () => {
    const { events, button } = await render(reviewable('PendingValidation'));

    button(VALIDATION_TEXTS.withdraw).click();

    expect(events.withdraw).toBe(1);
  });

  it('shows who validated it, how many people and offers to publish it', async () => {
    const { element, events, button, openDetails } = await render(
      reviewable('Validated', { validation: VALIDATION, validations: [VALIDATION] }),
    );

    expect(text(element.querySelector('.summary'))).toContain(
      VALIDATION_TEXTS.validated('23 de noviembre de 2026', 'Grupo de lectura (5 personas)'),
    );
    await openDetails();
    expect(text(element.querySelector('#validation-details'))).toContain('Se entiende bien.');
    button(VALIDATION_TEXTS.publish).click();

    expect(events.publish).toBe(1);
  });

  it('lists every round, newest first, with only the latest one open', async () => {
    const approved = {
      ...VALIDATION,
      round: 2,
      version: 2,
      notes: null,
      recordedAt: '2026-11-26T10:00:00Z',
    };
    const { element, openDetails } = await render(
      reviewable('Validated', { validation: approved, validations: [CHANGES, approved] }),
    );

    await openDetails();

    const heads = [...element.querySelectorAll<HTMLButtonElement>('.round-head')];
    expect(heads.map(text)).toEqual([
      `${VALIDATION_TEXTS.roundOutcomes.Approved} ${VALIDATION_TEXTS.roundLine(2, 2, '26 de noviembre de 2026', 'Grupo de lectura (5 personas)')}`,
      `${VALIDATION_TEXTS.roundOutcomes.ChangesRequested} ${VALIDATION_TEXTS.roundLine(1, 1, '23 de noviembre de 2026', 'Grupo de lectura (4 personas)')}2, ${VALIDATION_TEXTS.hardPartsCount(2)}, ${VALIDATION_TEXTS.hasNotes}`,
    ]);
    expect(heads.map((head) => head.getAttribute('aria-expanded'))).toEqual(['true', 'false']);
    expect(text(element.querySelector('#round-2'))).toBe(VALIDATION_TEXTS.roundEmpty);
    expect(element.querySelector('#round-1')).toBeNull();
  });

  it('opens an older round to show what the group asked for and its notes', async () => {
    const approved = { ...VALIDATION, round: 2, version: 2 };
    const { fixture, element, openDetails } = await render(
      reviewable('Validated', { validation: approved, validations: [CHANGES, approved] }),
    );
    await openDetails();

    const older = element.querySelectorAll<HTMLButtonElement>('.round-head')[1];
    expect(older.getAttribute('aria-controls')).toBe('round-1');
    older.click();
    await fixture.whenStable();

    const body = element.querySelector('#round-1')!;
    expect(older.getAttribute('aria-expanded')).toBe('true');
    expect([...body.querySelectorAll('.hard-list li')].map(text)).toEqual([
      '¿Cuánto dinero dan?',
      VALIDATION_TEXTS.glossaryPart,
    ]);
    expect(text(body)).toContain('Falta el plazo.');

    older.click();
    await fixture.whenStable();
    expect(element.querySelector('#round-1')).toBeNull();
  });

  it('offers no details before any round has been recorded', async () => {
    const { element } = await render(reviewable('PendingValidation'));

    expect(element.querySelector('.toggle')).toBeNull();
  });

  it('says a published document is the final version and offers no more steps', async () => {
    const { element } = await render(
      reviewable('Published', { validation: VALIDATION, validations: [VALIDATION] }),
    );

    expect(text(element.querySelector('.summary'))).toContain(VALIDATION_TEXTS.published);
    expect(element.querySelector('.actions button')).toBeNull();
  });
});
