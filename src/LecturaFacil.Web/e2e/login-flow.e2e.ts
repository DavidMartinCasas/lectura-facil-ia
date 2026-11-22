// SPDX-License-Identifier: Apache-2.0
// Flujo de la US-15: entrar → subir un documento → salir → ya no se puede ver nada. La API se simula con una sesión
// que abre el login y cierra el logout, como haría la cookie del backend. El E2E contra el backend real queda para la
// CI (US-19).
import { Page, expect, test } from '@playwright/test';
import { documentFixture } from '../src/app/testing/document-fixtures';
import { E2E_USER } from './session';

const DOCUMENT_ID = '7f1c2a9e-0000-4000-8000-000000000015';
const NOW = '2026-11-16T10:00:00Z';
const PASSWORD = 'contraseña-solo-para-el-e2e';
const XSRF_TOKEN = 'token-antiforgery-e2e';

interface FakeBackend {
  signedIn: boolean;
  xsrfHeaders: (string | undefined)[];
}

async function mockBackend(page: Page): Promise<FakeBackend> {
  const backend: FakeBackend = { signedIn: false, xsrfHeaders: [] };
  const unauthorized = {
    status: 401,
    contentType: 'application/problem+json',
    body: '{"status":401}',
  };

  // El backend deja esta cookie en cualquier GET a /api; Angular la copia en la cabecera X-XSRF-TOKEN.
  await page
    .context()
    .addCookies([{ name: 'XSRF-TOKEN', value: XSRF_TOKEN, url: 'http://localhost:4300' }]);

  await page.route('**/api/auth/me', (route) =>
    backend.signedIn ? route.fulfill({ json: { email: E2E_USER } }) : route.fulfill(unauthorized),
  );

  await page.route('**/api/auth/login', (route) => {
    backend.xsrfHeaders.push(route.request().headers()['x-xsrf-token']);
    const body = route.request().postDataJSON() as { email: string; password: string };
    if (body.email !== E2E_USER || body.password !== PASSWORD) {
      return route.fulfill({
        status: 401,
        contentType: 'application/problem+json',
        body: JSON.stringify({
          status: 401,
          detail: 'Correo o contraseña incorrectos.',
          code: 'auth.invalid_credentials',
        }),
      });
    }
    backend.signedIn = true;
    return route.fulfill({ json: { email: E2E_USER } });
  });

  await page.route('**/api/auth/logout', (route) => {
    backend.xsrfHeaders.push(route.request().headers()['x-xsrf-token']);
    backend.signedIn = false;
    return route.fulfill({ status: 204 });
  });

  await page.route('**/api/documents', (route) => {
    if (!backend.signedIn) {
      return route.fulfill(unauthorized);
    }
    if (route.request().method() === 'POST') {
      backend.xsrfHeaders.push(route.request().headers()['x-xsrf-token']);
      return route.fulfill({ status: 202, json: { id: DOCUMENT_ID, status: 'Received' } });
    }
    return route.fulfill({ json: { items: [], nextCursor: null } });
  });

  await page.route(`**/api/documents/${DOCUMENT_ID}/events`, (route) =>
    backend.signedIn
      ? route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: `event: status\ndata: ${JSON.stringify({ id: DOCUMENT_ID, status: 'Draft' })}\n\n`,
        })
      : route.fulfill(unauthorized),
  );

  await page.route(`**/api/documents/${DOCUMENT_ID}`, (route) =>
    backend.signedIn
      ? route.fulfill({
          json: documentFixture('Draft', { id: DOCUMENT_ID, createdAt: NOW, updatedAt: NOW }),
        })
      : route.fulfill(unauthorized),
  );

  return backend;
}

async function logIn(page: Page, password: string): Promise<void> {
  await page.getByLabel('Correo').fill(E2E_USER);
  await page.getByLabel('Contraseña').fill(password);
  await page.getByRole('button', { name: 'Entrar' }).click();
}

test('entra, sube un documento, sale y ya no puede ver sus documentos', async ({ page }) => {
  const backend = await mockBackend(page);

  await page.goto('/documentos');
  await expect(page).toHaveURL(/\/entrar\?volver=%2Fdocumentos$/);

  await logIn(page, PASSWORD);
  await expect(page).toHaveURL(/\/documentos$/);
  await expect(page.getByText(`Has entrado como ${E2E_USER}`)).toBeVisible();

  await page.goto('/nuevo');
  await page
    .getByLabel('Texto que quieres adaptar')
    .fill('La persona solicitante deberá presentar la solicitud en el plazo de diez días hábiles.');
  await page.getByRole('button', { name: 'Adaptar' }).click();
  await expect(page).toHaveURL(new RegExp(`/documentos/${DOCUMENT_ID}$`));

  await page.getByRole('button', { name: 'Salir' }).click();
  await expect(page).toHaveURL(/\/entrar$/);

  await page.goto(`/documentos/${DOCUMENT_ID}`);
  await expect(page).toHaveURL(/\/entrar\?volver=/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Entrar');
  expect(backend.xsrfHeaders).toEqual([XSRF_TOKEN, XSRF_TOKEN, XSRF_TOKEN]);
});

test('con una contraseña incorrecta muestra un mensaje genérico y no entra', async ({ page }) => {
  await mockBackend(page);

  await page.goto('/entrar');
  await logIn(page, 'otra-contraseña');

  await expect(page.getByRole('alert')).toHaveText('Correo o contraseña incorrectos.');
  await expect(page).toHaveURL(/\/entrar$/);
  await expect(page.getByLabel('Contraseña')).toHaveValue('');
});
