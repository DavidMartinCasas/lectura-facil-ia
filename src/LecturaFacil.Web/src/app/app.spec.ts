// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { App } from './app';
import { AuthService } from './core/auth/auth.service';
import { THEME_KEY, ThemeService } from './core/theme/theme.service';

@Component({ template: '' })
class EmptyPage {}

describe('App', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'entrar', component: EmptyPage }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
  });

  it('shows the project name in the top bar', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('header .brand')?.textContent).toContain('Lectura Fácil IA');
    expect(element.querySelector('main')).not.toBeNull();
  });

  it('shows the configured notice as a banner on every screen', async () => {
    const controller = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(App);
    flushConfig(controller, 'Instancia de demostración: no subas datos personales.');
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.site-notice')?.textContent).toContain(
      'Instancia de demostración: no subas datos personales.',
    );
    controller.verify();
  });

  it('shows no banner when the notice is empty', async () => {
    const controller = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(App);
    flushConfig(controller, '   ');
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('.site-notice')).toBeNull();
  });

  it('links to the samples page', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const links = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('nav a'));

    expect(links.some((link) => link.getAttribute('href') === '/ejemplos')).toBe(true);
  });

  it('hides the menu on the login screen', async () => {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/entrar?volver=%2Fdocumentos');
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('header.topbar')).toBeNull();
    expect(element.querySelector('main')).not.toBeNull();
  });

  it('changes the theme from the top bar and says which one is on', async () => {
    localStorage.removeItem(THEME_KEY);
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const button = Array.from(element.querySelectorAll('button')).find((candidate) =>
      candidate.textContent?.includes('Cambiar el tema'),
    )!;

    expect(button.textContent).toContain('Tema: Sistema');

    button.click();
    await fixture.whenStable();
    expect(button.textContent).toContain('Tema: Claro');

    button.click();
    await fixture.whenStable();
    expect(button.textContent).toContain('Tema: Oscuro');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');

    TestBed.inject(ThemeService).set('system');
  });

  it('shows who has logged in and a "Salir" button that closes the session', async () => {
    const controller = TestBed.inject(HttpTestingController);
    const auth = TestBed.inject(AuthService);
    const login = firstValueFrom(auth.login('persona@example.org', 'una-contraseña'));
    controller.expectOne('/api/auth/login').flush({ email: 'persona@example.org' });
    await login;
    const fixture = TestBed.createComponent(App);
    flushConfig(controller);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('persona@example.org');
    const logout = Array.from(element.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Salir'),
    );
    logout!.click();
    controller.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(auth.user()).toBeNull();
    expect(element.textContent).not.toContain('persona@example.org');
    controller.verify();
  });

  it('says so and keeps the session when the server cannot close it', async () => {
    const controller = TestBed.inject(HttpTestingController);
    const auth = TestBed.inject(AuthService);
    const login = firstValueFrom(auth.login('persona@example.org', 'una-contraseña'));
    controller.expectOne('/api/auth/login').flush({ email: 'persona@example.org' });
    await login;
    const fixture = TestBed.createComponent(App);
    flushConfig(controller);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    Array.from(element.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Salir'))!
      .click();
    controller.expectOne('/api/auth/logout').flush(null, { status: 500, statusText: 'Error' });
    await fixture.whenStable();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'No se ha podido cerrar la sesión',
    );
    expect(auth.user()).toEqual({ email: 'persona@example.org' });
    controller.verify();
  });
});

function flushConfig(controller: HttpTestingController, notice = ''): void {
  controller
    .expectOne('/api/config/public')
    .flush({ notice, turnstileEnabled: false, turnstileSiteKey: '' });
}
