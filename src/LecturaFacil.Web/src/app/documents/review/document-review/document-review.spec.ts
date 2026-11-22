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

async function renderFixture(smallScreen = false) {
  TestBed.configureTestingModule({
    providers: [
      {
        provide: BreakpointObserver,
        useValue: { observe: () => of<BreakpointState>({ matches: smallScreen, breakpoints: {} }) },
      },
    ],
  });
  const fixture = TestBed.createComponent(DocumentReviewComponent);
  fixture.componentRef.setInput('document', draft);
  await fixture.whenStable();
  return { fixture, element: fixture.nativeElement as HTMLElement };
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

  it('shows original and adaptation side by side on wide screens', async () => {
    const element = await render(false);

    expect(element.querySelector('.columns app-original-text')).not.toBeNull();
    expect(element.querySelector('.columns app-adapted-text')).not.toBeNull();
    expect(element.querySelector('mat-tab-group')).toBeNull();
    expect(element.querySelector('.banner app-export-panel')).not.toBeNull();
  });

  it('opens the review in a side panel, with the parts that have warnings unfolded', async () => {
    const element = await render(false);

    const toggle = element.querySelector<HTMLButtonElement>('.banner .review-toggle')!;
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(toggle.textContent).toContain('2 avisos');
    const panel = element.querySelector('.workarea aside#review-panel')!;
    expect(panel.getAttribute('aria-label')).toBe(REVIEW_TEXTS.reviewLabel);

    const heads = [...panel.querySelectorAll<HTMLButtonElement>('.part-head')];
    expect(heads.map((head) => head.querySelector('.figure')?.textContent?.trim())).toEqual([
      '1 aviso',
      '11 de 12',
    ]);
    expect(heads.map((head) => head.getAttribute('aria-expanded'))).toEqual(['true', 'true']);
    expect(panel.querySelector('#review-part-facts app-key-facts-panel')?.textContent).toContain(
      '1 conservado',
    );
    expect(panel.querySelector('app-guidelines-panel')).not.toBeNull();
    // La legibilidad, fija arriba y sin plegar.
    expect(panel.querySelector('.panel-top app-readability-panel')?.textContent).toContain(
      'Bastante fácil',
    );
  });

  it('leaves the texts the whole width when the panel is closed', async () => {
    const { fixture, element } = await renderFixture();
    const toggle = element.querySelector<HTMLButtonElement>('.review-toggle')!;

    toggle.click();
    await fixture.whenStable();

    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('#review-panel')).toBeNull();
    expect(element.querySelector('.workarea--open')).toBeNull();
  });

  it('starts closed when there is nothing to warn about', async () => {
    const element = await render(false, {
      ...draft,
      adaptation: adaptationFixture({
        pendingKeyFactCount: 0,
        canExport: true,
        guidelines: { ...adaptationFixture().guidelines!, compliantCount: 12 },
      }),
    });

    expect(element.querySelector('.review-toggle')?.getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('.review-toggle')?.textContent).toContain(REVIEW_TEXTS.noWarnings);
  });

  it('folds and unfolds each part of the review', async () => {
    const { fixture, element } = await renderFixture();
    const head = (index: number) =>
      element.querySelectorAll<HTMLButtonElement>('.part-head')[index];

    head(1).click();
    await fixture.whenStable();
    expect(head(1).getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('app-guidelines-panel')).toBeNull();

    head(1).click();
    await fixture.whenStable();
    expect(head(1).getAttribute('aria-expanded')).toBe('true');
    expect(element.querySelector('app-guidelines-panel')).not.toBeNull();

    head(0).click();
    await fixture.whenStable();
    expect(head(0).getAttribute('aria-expanded')).toBe('false');
    expect(element.querySelector('app-key-facts-panel')).toBeNull();
  });

  it('points out a key fact in the original until the key facts are folded', async () => {
    const { fixture, element } = await renderFixture();

    element.querySelector<HTMLButtonElement>('button.locate')!.click();
    await fixture.whenStable();
    expect(element.querySelector('app-original-text .spot')).not.toBeNull();
    // En pantallas anchas el panel sigue abierto.
    expect(element.querySelector('#review-panel')).not.toBeNull();

    element.querySelectorAll<HTMLButtonElement>('.part-head')[0].click();
    await fixture.whenStable();
    expect(element.querySelector('app-original-text .spot')).toBeNull();
  });

  it('on small screens opens the panel over the texts, and closes it to show a key fact', async () => {
    const { fixture, element } = await renderFixture(true);
    const toggle = element.querySelector<HTMLButtonElement>('.review-toggle')!;
    expect(toggle.getAttribute('aria-expanded')).toBe('false');

    toggle.click();
    await fixture.whenStable();
    expect(element.querySelector('#review-panel.review-panel--overlay')).not.toBeNull();
    expect(element.querySelector('.backdrop')).not.toBeNull();

    element.querySelector<HTMLButtonElement>('button.locate')!.click();
    await fixture.whenStable();
    expect(element.querySelector('#review-panel')).toBeNull();
    expect(element.querySelector('app-original-text .spot')).not.toBeNull();
  });

  it('closes the panel with its close button or the Escape key', async () => {
    const { fixture, element } = await renderFixture();

    element.querySelector<HTMLButtonElement>('.panel-close')!.click();
    await fixture.whenStable();
    expect(element.querySelector('#review-panel')).toBeNull();

    element.querySelector<HTMLButtonElement>('.review-toggle')!.click();
    await fixture.whenStable();
    element
      .querySelector('#review-panel')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await fixture.whenStable();
    expect(element.querySelector('#review-panel')).toBeNull();
  });

  it('uses tabs instead of columns on small screens', async () => {
    const element = await render(true);

    expect(element.querySelector('mat-tab-group')).not.toBeNull();
    expect(element.querySelector('.columns')).toBeNull();
    const tabs = [...element.querySelectorAll('mat-tab-group [role="tab"]')].map((tab) =>
      tab.textContent?.trim(),
    );
    expect(tabs).toEqual([REVIEW_TEXTS.originalTitle, REVIEW_TEXTS.adaptedTitle]);
  });

  it('keeps the panel and its parts as they were when the same document changes', async () => {
    const { fixture, element } = await renderFixture();

    fixture.componentRef.setInput('document', {
      ...draft,
      adaptation: adaptationFixture({ pendingKeyFactCount: 0, canExport: true }),
    });
    await fixture.whenStable();

    expect(
      element.querySelector('#review-panel #review-part-facts app-key-facts-panel'),
    ).not.toBeNull();
  });
});
