// SPDX-License-Identifier: Apache-2.0
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { NEVER, Observable, Subject, of, throwError } from 'rxjs';
import { PublicConfigService } from '../core/config/public-config.service';
import { DocumentEventsService } from '../core/documents/document-events.service';
import { DocumentSubmission } from '../core/documents/document.models';
import { DocumentsApiService } from '../core/documents/documents-api.service';
import { SampleText, SamplesApiService } from '../core/samples/samples-api.service';
import { TurnstileService } from '../core/turnstile/turnstile.service';
import { SAMPLES_TEXTS, TURNSTILE_TEXTS } from '../core/ui-texts';
import { FakeTurnstile } from '../testing/fake-turnstile';
import { SamplesPage } from './samples-page';

@Component({ template: '' })
class EmptyPage {}

const SAMPLES: readonly SampleText[] = [
  { id: 'uno', title: 'Ayudas', source: 'Texto ficticio', text: 'Texto del primer ejemplo.' },
  { id: 'dos', title: 'Requerimiento', source: 'Texto ficticio', text: 'Texto del segundo.' },
  { id: 'tres', title: 'Transporte', source: 'Texto ficticio', text: 'Texto del tercero.' },
];

describe('SamplesPage', () => {
  let submitted: DocumentSubmission[];
  let turnstile: FakeTurnstile;

  function setUp(options: {
    samples?: Observable<readonly SampleText[]>;
    siteKey?: string | null;
    accepted?: Observable<{ id: string; status: 'Received' }>;
  }): ComponentFixture<SamplesPage> {
    submitted = [];
    turnstile = new FakeTurnstile();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'documentos/:id', component: EmptyPage }]),
        { provide: SamplesApiService, useValue: { list: () => options.samples ?? of(SAMPLES) } },
        {
          provide: DocumentsApiService,
          useValue: {
            submit: (submission: DocumentSubmission) => {
              submitted.push(submission);
              return options.accepted ?? of({ id: 'doc-1', status: 'Received' as const });
            },
          },
        },
        // El progreso no termina nunca: la página no espera al final para abrir el documento.
        { provide: DocumentEventsService, useValue: { watchProgress: () => NEVER } },
        {
          provide: PublicConfigService,
          useValue: { turnstileSiteKey: signal(options.siteKey ?? null) },
        },
        { provide: TurnstileService, useValue: { load: () => Promise.resolve(turnstile) } },
      ],
    });
    return TestBed.createComponent(SamplesPage);
  }

  async function settle(fixture: ComponentFixture<SamplesPage>): Promise<void> {
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    await fixture.whenStable();
  }

  function adaptButtons(fixture: ComponentFixture<SamplesPage>): HTMLButtonElement[] {
    return Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('li button'),
    );
  }

  it('lists the three samples with their source', async () => {
    const fixture = setUp({});
    await settle(fixture);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelectorAll('li h2').length).toBe(3);
    expect(element.querySelector('.source')?.textContent).toContain('Texto ficticio');
    expect(adaptButtons(fixture)[1].getAttribute('aria-label')).toBe(
      SAMPLES_TEXTS.adaptLabel('Requerimiento'),
    );
  });

  it('launches the adaptation of a sample with one click', async () => {
    const fixture = setUp({});
    await settle(fixture);

    adaptButtons(fixture)[1].click();
    await settle(fixture);

    expect(submitted).toEqual([
      { kind: 'text', text: 'Texto del segundo.', title: 'Requerimiento' },
    ]);
    expect(TestBed.inject(Router).url).toBe('/documentos/doc-1');
  });

  it('marks only the pressed sample as being sent and shows no progress here', async () => {
    const accepted = new Subject<{ id: string; status: 'Received' }>();
    const fixture = setUp({ accepted });
    await settle(fixture);

    adaptButtons(fixture)[2].click();
    await settle(fixture);

    const labels = adaptButtons(fixture).map((button) => button.textContent?.trim());
    expect(labels).toEqual([SAMPLES_TEXTS.adapt, SAMPLES_TEXTS.adapt, SAMPLES_TEXTS.submitting]);
    expect((fixture.nativeElement as HTMLElement).querySelector('app-progress-steps')).toBeNull();

    accepted.next({ id: 'doc-3', status: 'Received' });
    await settle(fixture);
    expect(TestBed.inject(Router).url).toBe('/documentos/doc-3');
  });

  it('says so when the samples cannot be loaded', async () => {
    const fixture = setUp({ samples: throwError(() => new Error('caído')) });
    await settle(fixture);

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent,
    ).toContain(SAMPLES_TEXTS.loadFailed);
  });

  it('asks to complete the challenge before adapting when it is enabled', async () => {
    const fixture = setUp({ siteKey: 'clave-publica' });
    await settle(fixture);

    adaptButtons(fixture)[0].click();
    await settle(fixture);

    expect(submitted).toEqual([]);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent,
    ).toContain(TURNSTILE_TEXTS.required);

    turnstile.solve('token-1');
    await settle(fixture);
    adaptButtons(fixture)[0].click();
    await settle(fixture);

    expect(submitted).toEqual([
      { kind: 'text', text: 'Texto del primer ejemplo.', title: 'Ayudas', humanToken: 'token-1' },
    ]);
    expect(turnstile.resets).toBe(1);
  });
});
