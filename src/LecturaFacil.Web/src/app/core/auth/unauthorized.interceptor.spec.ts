// SPDX-License-Identifier: Apache-2.0
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { problemDetailsInterceptor } from '../errors/problem-details.interceptor';
import { AuthService } from './auth.service';
import { unauthorizedInterceptor } from './unauthorized.interceptor';

@Component({ template: '' })
class EmptyPage {}

describe('unauthorizedInterceptor', () => {
  let http: HttpClient;
  let router: Router;
  let controller: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([problemDetailsInterceptor, unauthorizedInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'entrar', component: EmptyPage },
          { path: 'documentos', component: EmptyPage },
        ]),
      ],
    });
    http = TestBed.inject(HttpClient);
    router = TestBed.inject(Router);
    controller = TestBed.inject(HttpTestingController);
    await router.navigateByUrl('/documentos');
  });

  afterEach(() => controller.verify());

  it('closes the session and goes to the login when the API answers 401', async () => {
    const request = firstValueFrom(http.get('/api/documents'));
    controller.expectOne('/api/documents').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(request).rejects.toMatchObject({ status: 401 });
    await TestBed.inject(Router).navigated;
    await new Promise((resolve) => setTimeout(resolve));
    expect(TestBed.inject(AuthService).user()).toBeNull();
    expect(router.url).toBe('/entrar?volver=%2Fdocumentos');
  });

  it('does not leave the page when the login itself answers 401', async () => {
    const request = firstValueFrom(http.post('/api/auth/login', {}));
    controller
      .expectOne('/api/auth/login')
      .flush(
        { status: 401, code: 'auth.invalid_credentials' },
        { status: 401, statusText: 'Unauthorized' },
      );

    await expect(request).rejects.toMatchObject({ message: 'Correo o contraseña incorrectos.' });
    expect(router.url).toBe('/documentos');
  });
});
