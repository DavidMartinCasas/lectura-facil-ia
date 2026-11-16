// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { problemDetailsInterceptor } from '../core/errors/problem-details.interceptor';
import { LoginPage } from './login-page';

@Component({ template: '' })
class EmptyPage {}

describe('LoginPage', () => {
  let fixture: ComponentFixture<LoginPage>;
  let controller: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'documentos', component: EmptyPage },
          { path: 'nuevo', component: EmptyPage },
        ]),
      ],
    });
    controller = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(LoginPage);
    controller.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();
  });

  afterEach(() => controller.verify());

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  async function fillAndSubmit(email: string, password: string): Promise<void> {
    const emailInput = element().querySelector<HTMLInputElement>('input[name="email"]')!;
    const passwordInput = element().querySelector<HTMLInputElement>('input[name="password"]')!;
    emailInput.value = email;
    emailInput.dispatchEvent(new Event('input'));
    passwordInput.value = password;
    passwordInput.dispatchEvent(new Event('input'));
    element().querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('has labelled email and password fields with the right autocomplete', () => {
    expect(element().querySelector('h1')?.textContent).toContain('Entrar');
    expect(element().querySelector('input[name="email"]')?.getAttribute('autocomplete')).toBe(
      'username',
    );
    expect(element().querySelector('input[name="password"]')?.getAttribute('type')).toBe(
      'password',
    );
  });

  it('goes to "Mis documentos" after logging in', async () => {
    await fillAndSubmit(' persona@example.org ', 'una-contraseña');
    const request = controller.expectOne('/api/auth/login');
    expect(request.request.body).toEqual({
      email: 'persona@example.org',
      password: 'una-contraseña',
    });
    request.flush({ email: 'persona@example.org' });
    await fixture.whenStable();

    expect(router.url).toBe('/documentos');
  });

  it('goes back to the page that was asked for', async () => {
    fixture.componentRef.setInput('volver', '/nuevo');
    await fillAndSubmit('persona@example.org', 'una-contraseña');
    controller.expectOne('/api/auth/login').flush({ email: 'persona@example.org' });
    await fixture.whenStable();

    expect(router.url).toBe('/nuevo');
  });

  it('shows a generic message when the credentials are wrong and clears the password', async () => {
    await fillAndSubmit('persona@example.org', 'mal');
    controller
      .expectOne('/api/auth/login')
      .flush(
        { status: 401, detail: 'Detalle interno', code: 'auth.invalid_credentials' },
        { status: 401, statusText: 'Unauthorized' },
      );
    await fixture.whenStable();

    const alert = element().querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('Correo o contraseña incorrectos.');
    expect(alert?.textContent).not.toContain('Detalle interno');
    expect(element().querySelector<HTMLInputElement>('input[name="password"]')?.value).toBe('');
  });

  it('asks for both fields without calling the API', async () => {
    await fillAndSubmit('', '');

    expect(element().querySelector('[role="alert"]')?.textContent).toContain(
      'Escribe tu correo y tu contraseña.',
    );
  });
});
