// SPDX-License-Identifier: Apache-2.0
import type { BrowserOptions, ErrorEvent } from '@sentry/angular';
import {
  MONITORING_TUNNEL_URL,
  REDACTED,
  initMonitoring,
  scrubBreadcrumb,
  scrubEvent,
  withoutTextBreadcrumbs,
} from './monitoring';

const DOCUMENT_TEXT = 'La Concejalía de Vivienda convoca ayudas para el alquiler.';
// La lista real la da la API (la misma que usa el backend); aquí, la parte que necesitan los tests.
const HINTS = ['text', 'content', 'original', 'adapted', 'email'];

describe('monitoring', () => {
  describe('initMonitoring', () => {
    it('starts Sentry through the tunnel, without personal data and with the environment and release', async () => {
      const started: BrowserOptions[] = [];

      const active = await initMonitoring(
        () =>
          Promise.resolve({
            dsn: 'https://clave@sentry.example.com/2',
            environment: 'pruebas',
            release: 'abc1234',
            sensitiveFields: HINTS,
          }),
        (options) => started.push(options),
      );

      expect(active).toBe(true);
      expect(started).toHaveLength(1);
      expect(started[0].dsn).toBe('https://clave@sentry.example.com/2');
      expect(started[0].environment).toBe('pruebas');
      expect(started[0].release).toBe('abc1234');
      expect(started[0].tunnel).toBe(MONITORING_TUNNEL_URL);
      expect(started[0].dataCollection).toEqual({
        userInfo: false,
        cookies: false,
        httpHeaders: false,
        httpBodies: [],
        urlQueryParams: false,
      });
      const sent = await started[0].beforeSend!(
        { type: undefined, extra: { text: DOCUMENT_TEXT } },
        {},
      );
      expect(sent?.extra).toEqual({ text: REDACTED });
    });

    it('does not start Sentry when the API gives no DSN', async () => {
      const start = vi.fn();

      const active = await initMonitoring(
        () =>
          Promise.resolve({
            dsn: null,
            environment: 'production',
            release: '1',
            sensitiveFields: HINTS,
          }),
        start,
      );

      expect(active).toBe(false);
      expect(start).not.toHaveBeenCalled();
    });

    it('does not start Sentry without the list of fields to scrub', async () => {
      const start = vi.fn();

      const active = await initMonitoring(
        () =>
          Promise.resolve({
            dsn: 'https://clave@sentry.example.com/2',
            environment: 'p',
            release: '1',
            sensitiveFields: [],
          }),
        start,
      );

      expect(active).toBe(false);
      expect(start).not.toHaveBeenCalled();
    });

    it('lets the application start when the configuration cannot be loaded', async () => {
      const start = vi.fn();

      const active = await initMonitoring(() => Promise.reject(new Error('sin red')), start);

      expect(active).toBe(false);
      expect(start).not.toHaveBeenCalled();
    });
  });

  describe('scrubEvent', () => {
    it('removes the request body, cookies, headers, query string and user', () => {
      const event: ErrorEvent = {
        type: undefined,
        request: {
          method: 'POST',
          url: 'https://app.example.com/documents/1?text=hola#fin',
          data: { text: DOCUMENT_TEXT },
          cookies: { sesion: 'abc' },
          headers: { Authorization: 'Bearer secreto' },
          query_string: 'email=ana@example.com',
        },
        user: { email: 'ana@example.com', ip_address: '203.0.113.7' },
      };

      const scrubbed = scrubEvent(event, HINTS);

      expect(scrubbed.request).toEqual({
        method: 'POST',
        url: 'https://app.example.com/documents/1',
      });
      expect(scrubbed.user).toBeUndefined();
      expect(JSON.stringify(scrubbed)).not.toContain('Concejalía');
      expect(JSON.stringify(scrubbed)).not.toContain('ana@example.com');
    });

    it.each(['text', 'content', 'original', 'adapted', 'originalText'])(
      'redacts extras, tags and contexts named "%s"',
      (field) => {
        const event: ErrorEvent = {
          type: undefined,
          extra: { [field]: DOCUMENT_TEXT, documentId: '8c1f' },
          tags: { [field]: DOCUMENT_TEXT, area: 'revision' },
          contexts: { [field]: { value: DOCUMENT_TEXT } },
        };

        const scrubbed = scrubEvent(event, HINTS);

        expect(scrubbed.extra).toEqual({ [field]: REDACTED, documentId: '8c1f' });
        expect(scrubbed.tags).toEqual({ area: 'revision' });
        expect(scrubbed.contexts).toEqual({});
      },
    );
  });

  describe('withoutTextBreadcrumbs', () => {
    it('drops console breadcrumbs and keeps the other default integrations', () => {
      const integrations = withoutTextBreadcrumbs([
        { name: 'Console' },
        { name: 'Breadcrumbs' },
        { name: 'GlobalHandlers' },
      ]);

      expect(integrations.map((integration) => integration.name)).toEqual([
        'GlobalHandlers',
        'Breadcrumbs',
      ]);
    });
  });

  describe('scrubBreadcrumb', () => {
    it('removes the query string from URLs and redacts text fields', () => {
      const scrubbed = scrubBreadcrumb(
        {
          category: 'fetch',
          data: { url: '/api/documents?text=hola', method: 'GET', content: DOCUMENT_TEXT },
        },
        HINTS,
      );

      expect(scrubbed.data).toEqual({ url: '/api/documents', method: 'GET', content: REDACTED });
    });
  });
});
