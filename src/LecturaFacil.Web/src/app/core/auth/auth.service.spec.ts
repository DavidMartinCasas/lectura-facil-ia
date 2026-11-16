// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let auth: AuthService;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('starts without knowing whether there is a session', () => {
    expect(auth.user()).toBeUndefined();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('keeps the user when /api/auth/me answers', async () => {
    const loaded = firstValueFrom(auth.loadSession());
    controller.expectOne('/api/auth/me').flush({ email: 'persona@example.org' });

    expect(await loaded).toBe(true);
    expect(auth.user()).toEqual({ email: 'persona@example.org' });
  });

  it('marks the session as closed when /api/auth/me answers 401', async () => {
    const loaded = firstValueFrom(auth.loadSession());
    controller.expectOne('/api/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(await loaded).toBe(false);
    expect(auth.user()).toBeNull();
  });

  it('sends only the email and the password to log in and keeps no token', async () => {
    const login = firstValueFrom(auth.login('persona@example.org', 'una-contraseña'));
    const request = controller.expectOne('/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'persona@example.org',
      password: 'una-contraseña',
    });
    request.flush({ email: 'persona@example.org' });

    await login;
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('closes the session in the browser even if the server fails', async () => {
    const login = firstValueFrom(auth.login('persona@example.org', 'una-contraseña'));
    controller.expectOne('/api/auth/login').flush({ email: 'persona@example.org' });
    await login;

    const logout = firstValueFrom(auth.logout());
    const request = controller.expectOne('/api/auth/logout');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 500, statusText: 'Error' });

    await logout;
    expect(auth.user()).toBeNull();
  });
});
