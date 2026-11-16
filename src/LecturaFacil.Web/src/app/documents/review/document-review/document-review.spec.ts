// SPDX-License-Identifier: Apache-2.0
import { BreakpointObserver, BreakpointState } from '@angular/cdk/layout';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ReviewableDocument } from '../../../core/documents/document.models';
import { REVIEW_TEXTS } from '../../../core/ui-texts';
import { adaptationFixture, documentFixture } from '../../../testing/document-fixtures';
import { DocumentReviewComponent } from './document-review';

const draft = documentFixture('Draft') as ReviewableDocument;

async function render(smallScreen: boolean, document: ReviewableDocument = draft) {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: BreakpointObserver,
        useValue: { observe: () => of<BreakpointState>({ matches: smallScreen, breakpoints: {} }) },
      },
    ],
  });
  const fixture = TestBed.createComponent(DocumentReviewComponent);
  fixture.componentRef.setInput('document', document);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('DocumentReviewComponent', () => {
  it('always shows the draft label, who processed it and why it cannot be exported yet', async () => {
    const element = await render(false);

    const banner = element.querySelector('.banner')!;
    expect(banner.textContent).toContain(REVIEW_TEXTS.draftLabel);
    expect(banner.textContent).toContain('Servidor propio');
    expect(banner.textContent).toContain(REVIEW_TEXTS.exportBlocked(1));
  });

  it('says it can be exported when every warning is acknowledged', async () => {
    const element = await render(false, {
      ...draft,
      adaptation: adaptationFixture({ pendingKeyFactCount: 0, canExport: true }),
    });

    expect(element.querySelector('.banner')?.textContent).toContain(REVIEW_TEXTS.exportReady);
  });

  it('shows original and adaptation side by side on wide screens, with every panel', async () => {
    const element = await render(false);

    expect(element.querySelector('.columns app-original-text')).not.toBeNull();
    expect(element.querySelector('.columns app-adapted-text')).not.toBeNull();
    expect(element.querySelector('mat-tab-group')).toBeNull();
    expect(element.querySelector('app-guidelines-panel')?.textContent).toContain(
      '11 de 12 pautas cumplidas',
    );
    expect(element.querySelector('app-readability-panel')?.textContent).toContain('Muy difícil');
    expect(element.querySelector('app-readability-panel')?.textContent).toContain('Bastante fácil');
    expect(element.querySelector('app-key-facts-panel')?.textContent).toContain('1 conservado');
  });

  it('uses tabs instead of columns on small screens', async () => {
    const element = await render(true);

    expect(element.querySelector('mat-tab-group')).not.toBeNull();
    expect(element.querySelector('.columns')).toBeNull();
    const tabs = [...element.querySelectorAll('[role="tab"]')].map((tab) =>
      tab.textContent?.trim(),
    );
    expect(tabs).toEqual([REVIEW_TEXTS.originalTitle, REVIEW_TEXTS.adaptedTitle]);
  });
});
