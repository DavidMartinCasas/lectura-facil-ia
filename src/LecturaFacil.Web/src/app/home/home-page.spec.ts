// SPDX-License-Identifier: Apache-2.0
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HomePage } from './home-page';

describe('HomePage', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('says what the application does in the main heading', async () => {
    const page = await render();

    expect(page.querySelector('h1')?.textContent?.trim()).toBe(
      'Convierte un texto administrativo en un texto que todo el mundo entiende.',
    );
  });

  it('warns that every adaptation is a draft pending human validation', async () => {
    const page = await render();

    expect(page.textContent).toContain('borrador');
    expect(page.textContent).toContain('validar una persona');
  });
});
