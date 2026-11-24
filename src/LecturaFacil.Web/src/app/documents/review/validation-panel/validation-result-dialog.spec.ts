// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { VALIDATION_TEXTS } from '../../../core/ui-texts';
import {
  ValidationResultDialogComponent,
  ValidationResultDialogData,
} from './validation-result-dialog';

async function render(
  data: ValidationResultDialogData = {
    sections: ['¿Qué es?', '¿Cuánto dinero dan?'],
    hasGlossary: true,
  },
) {
  const dialogRef = { close: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      { provide: MAT_DIALOG_DATA, useValue: data },
      { provide: MatDialogRef, useValue: dialogRef },
    ],
  });
  const fixture = TestBed.createComponent(ValidationResultDialogComponent);
  await fixture.whenStable();
  const element = fixture.nativeElement as HTMLElement;
  const submit = async () => {
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();
  };
  return { fixture, element, dialogRef, submit };
}

function type(element: HTMLElement, selector: string, value: string): void {
  const field = element.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
  field.value = value;
  field.dispatchEvent(new Event('input'));
}

function radios(element: HTMLElement): HTMLInputElement[] {
  return [...element.querySelectorAll<HTMLInputElement>('input[type="radio"]')];
}

function checkboxes(element: HTMLElement): HTMLInputElement[] {
  return [...element.querySelectorAll<HTMLInputElement>('input[type="checkbox"]')];
}

describe('ValidationResultDialogComponent', () => {
  it('asks for the outcome, who validated it and how many people before closing', async () => {
    const { element, dialogRef, submit } = await render();

    await submit();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(element.querySelector('#outcome-error')?.textContent).toContain(
      VALIDATION_TEXTS.outcomeRequired,
    );
    expect(element.textContent).toContain(VALIDATION_TEXTS.validatedByRequired);
    expect(element.querySelector('#participants-error')?.textContent).toContain(
      VALIDATION_TEXTS.participantsRequired,
    );
  });

  it('does not accept an impossible number of people', async () => {
    const { fixture, element, dialogRef, submit } = await render();

    radios(element)[0].click();
    type(element, 'input[name="validatedBy"]', 'Grupo');
    type(element, 'input[name="participants"]', '100');
    await fixture.whenStable();
    await submit();

    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(element.querySelector('#participants-error')).not.toBeNull();
  });

  it('closes with an approval, without hard parts or empty notes', async () => {
    const { fixture, element, dialogRef, submit } = await render();

    radios(element)[0].click();
    await fixture.whenStable();
    expect(checkboxes(element)).toEqual([]);
    type(element, 'input[name="validatedBy"]', '  Grupo de lectura  ');
    type(element, 'input[name="participants"]', '5');
    type(element, 'textarea[name="notes"]', '   ');
    await fixture.whenStable();
    await submit();

    expect(dialogRef.close).toHaveBeenCalledWith({
      outcome: 'Approved',
      validatedBy: 'Grupo de lectura',
      participants: 5,
      hardSections: [],
      hardGlossary: false,
      notes: null,
    });
  });

  it('marks the sections and the glossary that were hard when the group asks for changes', async () => {
    const { fixture, element, dialogRef, submit } = await render();

    radios(element)[1].click();
    await fixture.whenStable();
    const [first, second, glossary] = checkboxes(element);
    expect(element.querySelector('.hard-parts')?.textContent).toContain('¿Cuánto dinero dan?');
    second.click();
    first.click();
    glossary.click();
    type(element, 'input[name="validatedBy"]', 'Grupo');
    type(element, 'input[name="participants"]', '4');
    await fixture.whenStable();
    await submit();

    expect(dialogRef.close).toHaveBeenCalledWith({
      outcome: 'ChangesRequested',
      validatedBy: 'Grupo',
      participants: 4,
      hardSections: [0, 1],
      hardGlossary: true,
      notes: null,
    });
  });

  it('offers the glossary only when the adaptation has one', async () => {
    const { fixture, element } = await render({ sections: ['¿Qué es?'], hasGlossary: false });

    radios(element)[1].click();
    await fixture.whenStable();

    expect(checkboxes(element)).toHaveLength(1);
    expect(element.textContent).not.toContain(VALIDATION_TEXTS.glossaryPart);
  });
});
