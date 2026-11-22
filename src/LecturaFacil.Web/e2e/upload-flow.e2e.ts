// SPDX-License-Identifier: Apache-2.0
// Flujo de la US-12: subir texto → ver el progreso → llegar a la vista del documento. La API se simula con
// respuestas fijas (como haría el backend con FakeLlmProvider); el E2E contra el backend real queda para la CI (US-19).
import { Page, Route, expect, test } from '@playwright/test';
import { documentFixture } from '../src/app/testing/document-fixtures';
import { expectNoAxeViolations } from './axe';
import { mockSignedIn } from './session';

const DOCUMENT_ID = '7f1c2a9e-0000-4000-8000-000000000001';
const NOW = '2026-11-16T10:00:00Z';

function statusEvent(status: string): string {
  return `event: status\ndata: ${JSON.stringify({ id: DOCUMENT_ID, status })}\n\n`;
}

async function mockApi(
  page: Page,
  options: { failSubmitOnce?: boolean } = {},
): Promise<{ submissions: number }> {
  const counters = { submissions: 0 };
  let failNext = options.failSubmitOnce ?? false;
  await mockSignedIn(page);

  await page.route('**/api/documents', async (route: Route) => {
    if (route.request().method() === 'POST') {
      counters.submissions++;
      if (failNext) {
        failNext = false;
        return route.fulfill({
          status: 503,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            status: 503,
            detail: 'Npgsql timeout at db:5432',
            code: 'document.busy',
          }),
        });
      }
      return route.fulfill({ status: 202, json: { id: DOCUMENT_ID, status: 'Received' } });
    }
    return route.fulfill({
      json: {
        items: [
          {
            id: DOCUMENT_ID,
            title: 'Bases de la convocatoria',
            status: 'Draft',
            createdAt: NOW,
            updatedAt: NOW,
            provider: 'Servidor local',
          },
        ],
        nextCursor: null,
      },
    });
  });

  await page.route(`**/api/documents/${DOCUMENT_ID}/events`, (route) =>
    route.fulfill({
      status: 200,
      headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' },
      body: statusEvent('Received') + statusEvent('Processing') + statusEvent('Draft'),
    }),
  );

  await page.route(`**/api/documents/${DOCUMENT_ID}`, (route) =>
    route.fulfill({
      json: documentFixture('Draft', { id: DOCUMENT_ID, createdAt: NOW, updatedAt: NOW }),
    }),
  );

  return counters;
}

test('sube un texto, ve el progreso por pasos y llega a la vista del documento', async ({
  page,
}) => {
  await mockApi(page);
  await page.goto('/nuevo');

  await page.getByLabel('Título (opcional)').fill('Bases de la convocatoria');
  await page
    .getByLabel('Texto que quieres adaptar')
    .fill('La persona solicitante deberá presentar la solicitud en el plazo de diez días hábiles.');
  await page.getByRole('button', { name: 'Adaptar' }).click();

  await expect(page).toHaveURL(new RegExp(`/documentos/${DOCUMENT_ID}$`));
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Bases de la convocatoria');
  await expect(page.getByText('Estado: Borrador')).toBeVisible();
});

test('muestra el progreso con los cinco pasos', async ({ page }) => {
  await mockApi(page);
  // Sin el estado final, el progreso se queda a la vista.
  await page.route(`**/api/documents/${DOCUMENT_ID}/events`, (route) =>
    route.fulfill({
      status: 200,
      headers: { 'Content-Type': 'text/event-stream' },
      body: statusEvent('Processing'),
    }),
  );
  await page.goto('/nuevo');

  await page
    .getByLabel('Texto que quieres adaptar')
    .fill('Texto de prueba para adaptar a lectura fácil.');
  await page.getByRole('button', { name: 'Adaptar' }).click();

  const steps = page.getByRole('listitem');
  await expect(steps).toHaveText([
    /Recibido/,
    /Adaptando/,
    /Revisando pautas/,
    /Verificando/,
    /Listo/,
  ]);
  await expect(page.locator('[aria-current="step"]')).toContainText('Adaptando (en curso)');
});

test('rechaza en el navegador un fichero que no es PDF, sin llamar a la API', async ({ page }) => {
  const counters = await mockApi(page);
  await page.goto('/nuevo');

  await page.getByRole('radio', { name: 'Subir PDF' }).click();
  await page
    .getByLabel('Fichero PDF')
    .setInputFiles({ name: 'notas.pdf', mimeType: 'application/pdf', buffer: Buffer.from('hola') });
  await page.getByRole('button', { name: 'Adaptar' }).click();

  await expect(page.getByRole('alert')).toHaveText(
    'El fichero no es un PDF. Solo se admiten ficheros PDF con texto.',
  );
  expect(counters.submissions).toBe(0);
});

test('muestra un mensaje en español sin detalles técnicos y permite reintentar', async ({
  page,
}) => {
  const counters = await mockApi(page, { failSubmitOnce: true });
  await page.goto('/nuevo');

  await page
    .getByLabel('Texto que quieres adaptar')
    .fill('Texto de prueba para adaptar a lectura fácil.');
  await page.getByRole('button', { name: 'Adaptar' }).click();

  const alert = page.getByRole('alert');
  await expect(alert).toContainText('El servicio está ocupado ahora mismo.');
  await expect(alert).not.toContainText('Npgsql');
  await alert.getByRole('button', { name: 'Reintentar' }).click();

  await expect(page).toHaveURL(new RegExp(`/documentos/${DOCUMENT_ID}$`));
  expect(counters.submissions).toBe(2);
});

test('"Mis documentos" muestra título, fecha, estado y proveedor', async ({ page }) => {
  await mockApi(page);
  await page.goto('/documentos');

  const row = page.getByRole('row').nth(1);
  await expect(row).toContainText('Bases de la convocatoria');
  await expect(row).toContainText('2026');
  await expect(row).toContainText('Borrador');
  await expect(row).toContainText('Servidor local');
});

test('se completa el flujo solo con el teclado y el foco siempre se ve', async ({
  page,
  isMobile,
}) => {
  test.skip(isMobile, 'En móvil no hay teclado físico.');
  await mockApi(page);
  await page.goto('/nuevo');

  const focusOn = async (accessibleName: string) => {
    for (let presses = 0; presses < 30; presses++) {
      await page.keyboard.press('Tab');
      const focused = page.locator(':focus');
      const label = await focused.evaluate(
        (element) => element.getAttribute('aria-label') ?? element.textContent ?? '',
      );
      const outline = await focused.evaluate((element) => getComputedStyle(element).outlineStyle);
      expect(outline, `foco visible en «${label.trim()}»`).not.toBe('none');
      if (
        (await focused.evaluate((element) => element.tagName)) === 'TEXTAREA' &&
        accessibleName === 'textarea'
      ) {
        return;
      }
      if (label.trim() === accessibleName) {
        return;
      }
    }
    throw new Error(`No se llega con el tabulador a «${accessibleName}».`);
  };

  await focusOn('textarea');
  await page.keyboard.type('Texto de prueba para adaptar a lectura fácil.');
  await focusOn('Adaptar');
  await page.keyboard.press('Enter');

  await expect(page).toHaveURL(new RegExp(`/documentos/${DOCUMENT_ID}$`));
});

for (const colorScheme of ['light', 'dark'] as const) {
  test(`las tres pantallas pasan axe en tema ${colorScheme === 'light' ? 'claro' : 'oscuro'}`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme });
    await mockApi(page);

    for (const path of ['/nuevo', '/documentos', `/documentos/${DOCUMENT_ID}`]) {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await expectNoAxeViolations(page);
    }
  });
}
