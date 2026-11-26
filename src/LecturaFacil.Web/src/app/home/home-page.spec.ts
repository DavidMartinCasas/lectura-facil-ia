// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { HomePage } from './home-page';

describe('HomePage', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }),
  );

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(HomePage);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function actions(page: HTMLElement): { text: string; href: string | null }[] {
    return Array.from(page.querySelectorAll('.actions a')).map((link) => ({
      text: link.textContent?.trim() ?? '',
      href: link.getAttribute('href'),
    }));
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

  it('without a session, offers to adapt a document and to log in', async () => {
    TestBed.inject(AuthService).sessionExpired();

    const page = await render();

    expect(actions(page)).toEqual([
      { text: 'Adaptar un documento', href: '/nuevo' },
      { text: 'Entrar', href: '/entrar' },
    ]);
  });

  it('with a session, offers to adapt a document and to see my documents', async () => {
    const controller = TestBed.inject(HttpTestingController);
    const login = firstValueFrom(
      TestBed.inject(AuthService).login('persona@example.org', 'una-contraseña'),
    );
    controller.expectOne('/api/auth/login').flush({ email: 'persona@example.org' });
    await login;

    const page = await render();

    expect(actions(page)).toEqual([
      { text: 'Adaptar un documento', href: '/nuevo' },
      { text: 'Ver mis documentos', href: '/documentos' },
    ]);
  });

  it('while the session is unknown, shows only the main action', async () => {
    const page = await render();

    expect(actions(page)).toEqual([{ text: 'Adaptar un documento', href: '/nuevo' }]);
  });
});
