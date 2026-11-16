// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { OriginalFinding } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';
import { ORIGINAL_TEXT, documentFixture } from '../../../testing/document-fixtures';
import { OriginalTextComponent } from './original-text';

async function render(text: string, findings: readonly OriginalFinding[]) {
  const fixture = TestBed.createComponent(OriginalTextComponent);
  fixture.componentRef.setInput('text', text);
  fixture.componentRef.setInput('findings', findings);
  await fixture.whenStable();
  return { fixture, element: fixture.nativeElement as HTMLElement };
}

describe('OriginalTextComponent', () => {
  const findings = documentFixture('Draft').original.findings;

  it('shows the whole text, with each finding marked as a button', async () => {
    const { element } = await render(ORIGINAL_TEXT, findings);

    expect(element.querySelector('.text')?.textContent).toBe(ORIGINAL_TEXT);
    const marks = [...element.querySelectorAll<HTMLButtonElement>('button.mark')];
    expect(marks.map((mark) => mark.textContent)).toEqual(['40 %', 'BOP']);
    expect(marks[0].getAttribute('aria-pressed')).toBe('false');
  });

  it('shows the guideline and the suggestion of the selected fragment', async () => {
    const { fixture, element } = await render(ORIGINAL_TEXT, findings);

    element.querySelector<HTMLButtonElement>('button.mark')!.click();
    await fixture.whenStable();

    const detail = element.querySelector('.detail')!;
    expect(detail.textContent).toContain('Sin porcentajes');
    expect(detail.textContent).toContain('Explica la cantidad sin porcentajes.');
    expect(element.querySelector('button.mark')?.getAttribute('aria-pressed')).toBe('true');
  });

  it('lets the keyboard reach the fragments and close the explanation', async () => {
    const { fixture, element } = await render(ORIGINAL_TEXT, findings);
    const mark = element.querySelector<HTMLButtonElement>('button.mark')!;

    expect(mark.tabIndex).toBe(0);
    mark.focus();
    mark.click();
    await fixture.whenStable();
    [...element.querySelectorAll<HTMLButtonElement>('.detail button')]
      .find((button) => button.textContent?.includes(REVIEW_TEXTS.closeSelection))!
      .click();
    await fixture.whenStable();

    expect(element.querySelector('.detail')?.textContent?.trim()).toBe('');
  });

  it('shows both guidelines where two findings overlap', async () => {
    const overlapping: OriginalFinding[] = [
      { ...findings[0], start: 0, end: 8, fragment: 'La ayuda' },
      { ...findings[1], start: 3, end: 18, fragment: 'ayuda ascenderá' },
    ];
    const { fixture, element } = await render(ORIGINAL_TEXT, overlapping);

    const marks = [...element.querySelectorAll<HTMLButtonElement>('button.mark')];
    expect(marks.map((mark) => mark.textContent)).toEqual(['La ', 'ayuda', ' ascenderá']);
    marks[1].click();
    await fixture.whenStable();

    expect(element.querySelectorAll('.detail li')).toHaveLength(2);
  });

  it('shows text that looks like HTML literally, without creating elements', async () => {
    const text = 'Antes <script>alert(1)</script> <img src=x onerror=alert(2)> después.';
    const { element } = await render(text, [
      { ...findings[0], start: 6, end: 14, fragment: '<script>' },
    ]);

    expect(element.querySelector('.text')?.textContent).toBe(text);
    expect(element.querySelector('script')).toBeNull();
    expect(element.querySelector('img')).toBeNull();
    expect(element.querySelector('button.mark')?.textContent).toBe('<script>');
  });

  it('says so when the original has no findings', async () => {
    const { element } = await render('Un texto sin problemas.', []);

    expect(element.textContent).toContain(REVIEW_TEXTS.noOriginalFindings);
    expect(element.querySelector('button.mark')).toBeNull();
  });
});
