// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { MeaningVerification } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';
import { ORIGINAL_TEXT, adaptationFixture } from '../../../testing/document-fixtures';
import { KeyFactsPanelComponent } from './key-facts-panel';

const verification = adaptationFixture().verification!;

async function render(value: MeaningVerification | null, editable = true) {
  const fixture = TestBed.createComponent(KeyFactsPanelComponent);
  fixture.componentRef.setInput('verification', value);
  fixture.componentRef.setInput('editable', editable);
  const acknowledged: number[] = [];
  fixture.componentInstance.acknowledge.subscribe((id) => acknowledged.push(id));
  await fixture.whenStable();
  return { element: fixture.nativeElement as HTMLElement, acknowledged };
}

describe('KeyFactsPanelComponent', () => {
  it('highlights the warnings with their reason', async () => {
    const { element } = await render(verification);

    const warning = element.querySelector('.item--warning')!;
    expect(warning.textContent).toContain('Omitido');
    expect(warning.textContent).toContain('40 % de la renta mensual');
    expect(warning.textContent).toContain(REVIEW_TEXTS.notInAdaptation);
    expect(warning.textContent).toContain('No aparece en la adaptación');
    expect(element.querySelectorAll('.item--warning')).toHaveLength(1);
  });

  it('asks to acknowledge only the warnings that are pending', async () => {
    const { element, acknowledged } = await render(verification);

    const buttons = element.querySelectorAll<HTMLButtonElement>('button');
    expect(buttons).toHaveLength(1);
    buttons[0].click();

    expect(acknowledged).toEqual([0]);
  });

  it('keeps an acknowledged warning visible, marked as reviewed', async () => {
    const reviewed: MeaningVerification = {
      ...verification,
      keyFacts: verification.keyFacts.map((fact) =>
        fact.id === 0 ? { ...fact, acknowledged: true, requiresAcknowledgement: false } : fact,
      ),
    };

    const { element } = await render(reviewed);

    expect(element.querySelector('.item--done')?.textContent).toContain(REVIEW_TEXTS.acknowledged);
    expect(element.querySelector('.item--done')?.textContent).toContain(
      'No aparece en la adaptación',
    );
    expect(element.querySelector('button')).toBeNull();
  });

  it('does not offer to acknowledge when the document cannot be edited', async () => {
    const { element } = await render(verification, false);

    expect(element.querySelector('button')).toBeNull();
  });

  it('offers to point out in the original the values that are there', async () => {
    const fixture = TestBed.createComponent(KeyFactsPanelComponent);
    fixture.componentRef.setInput('verification', verification);
    fixture.componentRef.setInput('originalText', ORIGINAL_TEXT);
    const located: number[] = [];
    fixture.componentInstance.locate.subscribe((id) => located.push(id));
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    const locate = [...element.querySelectorAll<HTMLButtonElement>('button.locate')];
    expect(locate.map((button) => button.getAttribute('aria-label'))).toEqual(
      verification.keyFacts.map((fact) => `${REVIEW_TEXTS.locate}: ${fact.originalValue}`),
    );
    locate[0].click();
    expect(located).toEqual([verification.keyFacts[0].id]);

    fixture.componentRef.setInput('located', verification.keyFacts[0].id);
    await fixture.whenStable();
    expect(locate[0].getAttribute('aria-pressed')).toBe('true');
    expect(locate[0].textContent?.trim()).toBe(REVIEW_TEXTS.locateOff);
  });
});
