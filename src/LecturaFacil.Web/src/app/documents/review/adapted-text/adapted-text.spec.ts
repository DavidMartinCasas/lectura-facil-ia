// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { Adaptation, ManualEdit } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';
import { adaptationFixture } from '../../../testing/document-fixtures';
import { AdaptedTextComponent } from './adapted-text';

async function render(adaptation: Adaptation, editable = true) {
  const fixture = TestBed.createComponent(AdaptedTextComponent);
  fixture.componentRef.setInput('adaptation', adaptation);
  fixture.componentRef.setInput('editable', editable);
  const saved: ManualEdit[] = [];
  fixture.componentInstance.save.subscribe((edit) => saved.push(edit));
  await fixture.whenStable();
  return { fixture, element: fixture.nativeElement as HTMLElement, saved };
}

function buttonWithText(element: HTMLElement, text: string): HTMLButtonElement {
  return [...element.querySelectorAll<HTMLButtonElement>('button')].find((button) =>
    button.textContent?.includes(text),
  )!;
}

describe('AdaptedTextComponent', () => {
  it('shows the title, the sections and the glossary', async () => {
    const { element } = await render(adaptationFixture());

    expect(element.querySelector('h3')?.textContent).toBe('Ayudas para el alquiler');
    expect(element.querySelector('h4')?.textContent).toBe('¿Cuánto dinero dan?');
    expect([...element.querySelectorAll('li')].map((item) => item.textContent)).toEqual([
      'Le damos una parte del alquiler.',
      'Como mucho, 250 euros al mes.',
    ]);
    expect(element.querySelector('dt')?.textContent).toBe('BOP');
  });

  it('edits a section and sends the whole adaptation with that section changed', async () => {
    const { fixture, element, saved } = await render(adaptationFixture());

    buttonWithText(element, REVIEW_TEXTS.editSection).click();
    await fixture.whenStable();
    const textarea = element.querySelector('textarea')!;
    expect(textarea.value).toBe('Le damos una parte del alquiler.\nComo mucho, 250 euros al mes.');
    textarea.value = 'Le damos dinero para el alquiler.\n\n  Como mucho, 250 euros al mes.  ';
    textarea.dispatchEvent(new Event('input'));
    element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));

    expect(saved).toEqual([
      {
        title: 'Ayudas para el alquiler',
        sections: [
          {
            question: '¿Cuánto dinero dan?',
            sentences: ['Le damos dinero para el alquiler.', 'Como mucho, 250 euros al mes.'],
          },
        ],
        glossary: [{ term: 'BOP', definition: 'Boletín Oficial de la Provincia.' }],
      },
    ]);
  });

  it('does not save a section without sentences', async () => {
    const { fixture, element, saved } = await render(adaptationFixture());

    buttonWithText(element, REVIEW_TEXTS.editSection).click();
    await fixture.whenStable();
    const textarea = element.querySelector('textarea')!;
    textarea.value = '   ';
    textarea.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));

    expect(saved).toEqual([]);
    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      REVIEW_TEXTS.emptySection,
    );
  });

  it('closes the editor when a new version arrives', async () => {
    const { fixture, element } = await render(adaptationFixture());
    buttonWithText(element, REVIEW_TEXTS.editSection).click();
    await fixture.whenStable();

    fixture.componentRef.setInput('adaptation', adaptationFixture({ version: 2 }));
    await fixture.whenStable();

    expect(element.querySelector('textarea')).toBeNull();
  });

  it('cannot be edited when it is not a draft', async () => {
    const { element } = await render(adaptationFixture(), false);

    expect(buttonWithText(element, REVIEW_TEXTS.editSection)).toBeUndefined();
  });

  it('shows text that looks like HTML literally', async () => {
    const { element } = await render(
      adaptationFixture({
        sections: [{ question: '<b>¿Qué?</b>', sentences: ['<script>alert(1)</script>'] }],
      }),
    );

    expect(element.querySelector('li')?.textContent).toBe('<script>alert(1)</script>');
    expect(element.querySelector('script')).toBeNull();
    expect(element.querySelector('h4 b')).toBeNull();
  });
});
