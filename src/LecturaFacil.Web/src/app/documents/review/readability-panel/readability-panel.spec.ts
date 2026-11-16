// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { ReadabilityComparison } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';
import { documentFixture } from '../../../testing/document-fixtures';
import { ReadabilityPanelComponent } from './readability-panel';

async function render(readability: ReadabilityComparison | null) {
  const fixture = TestBed.createComponent(ReadabilityPanelComponent);
  fixture.componentRef.setInput('readability', readability);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('ReadabilityPanelComponent', () => {
  it('shows the score before and after, rounded, with its grade', async () => {
    const element = await render(documentFixture('Draft').readability);

    const scores = [...element.querySelectorAll('dd')].map((dd) =>
      dd.textContent?.replace(/\s+/g, ' ').trim(),
    );
    expect(scores).toEqual(['38 Muy difícil', '71 Bastante fácil']);
    expect(element.textContent).toContain('+33');
  });

  it('says so when it could not be calculated', async () => {
    const element = await render(null);

    expect(element.textContent).toContain(REVIEW_TEXTS.readabilityUnavailable);
  });
});
