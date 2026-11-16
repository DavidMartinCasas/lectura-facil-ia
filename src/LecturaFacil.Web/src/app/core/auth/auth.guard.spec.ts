// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authGuard, safeReturnUrl } from './auth.guard';

@Component({ template: '' })
class EmptyPage {}

describe('authGuard', () => {
  let router: Router;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'entrar', component: EmptyPage },
          { path: 'documentos', component: EmptyPage, canActivate: [authGuard] },
        ]),
      ],
    });
    router = TestBed.inject(Router);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('sends to the login, remembering where to come back, when there is no session', async () => {
    const navigation = router.navigateByUrl('/documentos');
    await new Promise((resolve) => setTimeout(resolve));
    controller.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await navigation;

    expect(router.url).toBe('/entrar?volver=%2Fdocumentos');
  });

  it('lets the user in when there is a session', async () => {
    const navigation = router.navigateByUrl('/documentos');
    await new Promise((resolve) => setTimeout(resolve));
    controller.expectOne('/api/auth/me').flush({ email: 'persona@example.org' });
    await navigation;

    expect(router.url).toBe('/documentos');
  });
});

describe('safeReturnUrl', () => {
  it('keeps internal routes', () => {
    expect(safeReturnUrl('/documentos/7')).toBe('/documentos/7');
  });

  it.each([undefined, null, '', 'https://example.org', '//example.org', '/\\example.org'])(
    'goes to "Mis documentos" instead of %s',
    (value) => {
      expect(safeReturnUrl(value)).toBe('/documentos');
    },
  );
});
