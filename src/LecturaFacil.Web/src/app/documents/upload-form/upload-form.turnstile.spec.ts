// SPDX-License-Identifier: Apache-2.0
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DocumentSubmission } from '../../core/documents/document.models';
import { TurnstileService } from '../../core/turnstile/turnstile.service';
import { TURNSTILE_TEXTS } from '../../core/ui-texts';
import { FakeTurnstile } from '../../testing/fake-turnstile';
import { UploadFormComponent } from './upload-form';

describe('UploadFormComponent with the anti-bot challenge', () => {
  let fixture: ComponentFixture<UploadFormComponent>;
  let element: HTMLElement;
  let emitted: DocumentSubmission[];
  let turnstile: FakeTurnstile;

  beforeEach(async () => {
    turnstile = new FakeTurnstile();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: TurnstileService, useValue: { load: () => Promise.resolve(turnstile) } },
      ],
    });
    fixture = TestBed.createComponent(UploadFormComponent);
    fixture.componentRef.setInput('turnstileSiteKey', 'clave-publica');
    emitted = [];
    fixture.componentInstance.submitted.subscribe((submission) => emitted.push(submission));
    await settle();
    element = fixture.nativeElement as HTMLElement;
    const textarea = element.querySelector('textarea')!;
    textarea.value = 'Texto que hay que adaptar.';
    textarea.dispatchEvent(new Event('input'));
    await settle();
  });

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await settle();
  }

  it('renders the widget with the site key', () => {
    expect(turnstile.options?.sitekey).toBe('clave-publica');
  });

  it('asks to complete the challenge and emits nothing without a token', async () => {
    await submit();

    expect(element.querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      TURNSTILE_TEXTS.required,
    );
    expect(emitted).toEqual([]);
  });

  it('emits the token with the submission and asks for a new challenge', async () => {
    turnstile.solve('token-1');
    await settle();

    await submit();

    expect(emitted).toEqual([
      { kind: 'text', text: 'Texto que hay que adaptar.', title: '', humanToken: 'token-1' },
    ]);
    expect(turnstile.resets).toBe(1);
  });

  it('removes the widget when the form goes away', () => {
    fixture.destroy();

    expect(turnstile.removed).toBe(1);
  });
});
