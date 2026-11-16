// SPDX-License-Identifier: Apache-2.0
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PublicConfigService } from './public-config.service';

describe('PublicConfigService', () => {
  let service: PublicConfigService;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PublicConfigService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('starts without notice and without challenge', () => {
    expect(service.notice()).toBe('');
    expect(service.turnstileSiteKey()).toBeNull();
  });

  it('loads the configuration only once', () => {
    service.load();
    service.load();

    controller
      .expectOne('/api/config/public')
      .flush({ notice: ' Aviso ', turnstileEnabled: true, turnstileSiteKey: 'clave-publica' });

    expect(service.notice()).toBe('Aviso');
    expect(service.turnstileSiteKey()).toBe('clave-publica');
  });

  it('ignores the site key when the challenge is disabled', () => {
    service.load();

    controller
      .expectOne('/api/config/public')
      .flush({ notice: '', turnstileEnabled: false, turnstileSiteKey: 'clave-publica' });

    expect(service.turnstileSiteKey()).toBeNull();
  });

  it('keeps the defaults when the server fails', () => {
    service.load();

    controller.expectOne('/api/config/public').flush(null, { status: 500, statusText: 'Error' });

    expect(service.notice()).toBe('');
    expect(service.turnstileSiteKey()).toBeNull();
  });
});
