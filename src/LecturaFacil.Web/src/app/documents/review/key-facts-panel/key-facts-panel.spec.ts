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

  it('says when only some parts of a long document could be verified', async () => {
    const { element } = await render({ ...verification, status: 'PartiallyVerified' });

    expect(element.textContent).toContain(REVIEW_TEXTS.partiallyVerified);
    expect(element.textContent).not.toContain(REVIEW_TEXTS.notVerified);
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

  describe('acknowledging every warning at once', () => {
    // Los dos datos con aviso: así se ofrece marcarlos todos.
    const twoWarnings: MeaningVerification = {
      ...verification,
      keyFacts: verification.keyFacts.map((fact) => ({
        ...fact,
        status: 'Review',
        acknowledged: false,
        requiresAcknowledgement: true,
      })),
    };

    async function renderAll(value: MeaningVerification, editable = true) {
      const fixture = TestBed.createComponent(KeyFactsPanelComponent);
      fixture.componentRef.setInput('verification', value);
      fixture.componentRef.setInput('editable', editable);
      let requested = 0;
      fixture.componentInstance.acknowledgeAll.subscribe(() => requested++);
      await fixture.whenStable();
      const element = fixture.nativeElement as HTMLElement;
      document.body.appendChild(element);
      return { fixture, element, requested: () => requested };
    }

    afterEach(() => document.body.replaceChildren());

    it('is offered only with two warnings or more, and only while editable', async () => {
      expect((await render(verification)).element.querySelector('.acknowledge-all')).toBeNull();
      expect(
        (await renderAll(twoWarnings, false)).element.querySelector('.acknowledge-all'),
      ).toBeNull();

      const { element } = await renderAll(twoWarnings);
      expect(element.querySelector('.acknowledge-all')?.textContent?.trim()).toBe(
        REVIEW_TEXTS.acknowledgeAll(2),
      );
    });

    it('asks for confirmation first, with the focus on cancel', async () => {
      const { fixture, element, requested } = await renderAll(twoWarnings);

      element.querySelector<HTMLButtonElement>('.acknowledge-all')!.click();
      await fixture.whenStable();

      const confirm = element.querySelector('.confirm')!;
      expect(confirm.textContent).toContain(REVIEW_TEXTS.acknowledgeAllConfirm(2));
      expect(document.activeElement?.textContent?.trim()).toBe(REVIEW_TEXTS.cancel);
      expect(requested()).toBe(0);
    });

    it('marks them all once confirmed', async () => {
      const { fixture, element, requested } = await renderAll(twoWarnings);
      element.querySelector<HTMLButtonElement>('.acknowledge-all')!.click();
      await fixture.whenStable();

      const confirmButton = [
        ...element.querySelectorAll<HTMLButtonElement>('.confirm button'),
      ].find(
        (button) => button.textContent?.trim() === REVIEW_TEXTS.acknowledgeAllConfirmButton(2),
      )!;
      confirmButton.click();
      await fixture.whenStable();

      expect(requested()).toBe(1);
      expect(element.querySelector('.confirm')).toBeNull();
    });

    it('marks nothing when cancelled, and gives the focus back', async () => {
      const { fixture, element, requested } = await renderAll(twoWarnings);
      element.querySelector<HTMLButtonElement>('.acknowledge-all')!.click();
      await fixture.whenStable();

      (document.activeElement as HTMLButtonElement).click();
      await fixture.whenStable();

      expect(requested()).toBe(0);
      expect(element.querySelector('.confirm')).toBeNull();
      expect(document.activeElement).toBe(element.querySelector('.acknowledge-all'));
    });
  });
});
