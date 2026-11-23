// SPDX-License-Identifier: Apache-2.0
// "Mis documentos" con muchos documentos: buscar por título, filtrar por estado y cargar más páginas. La API se simula
// con las mismas reglas que el backend (sin distinguir mayúsculas ni tildes, páginas por cursor).
import { Page, Request, expect, test } from '@playwright/test';
import { DocumentListItem, DocumentStatus } from '../src/app/core/documents/document.models';
import { expectNoAxeViolations } from './axe';
import { mockSignedIn } from './session';

const PAGE_SIZE = 20;

const TITLES = ['Bases de la convocatoria', 'Constitución Española', 'Ayudas al alquiler'];
const STATUSES: DocumentStatus[] = ['Draft', 'Processing', 'Error'];

/** 45 documentos: tres páginas sin filtros. */
const DOCUMENTS: DocumentListItem[] = Array.from({ length: 45 }, (_, index) => {
  const createdAt = new Date(Date.UTC(2026, 10, 20, 12, 0) - index * 60_000).toISOString();
  return {
    id: `00000000-0000-4000-8000-${String(index).padStart(12, '0')}`,
    title: `${TITLES[index % TITLES.length]} ${index + 1}`,
    status: STATUSES[Math.floor(index / TITLES.length) % STATUSES.length],
    createdAt,
    updatedAt: createdAt,
    provider: 'Servidor local',
    warningCount: 0,
    readability: null,
  };
});

const normalize = (value: string): string =>
  value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase();

async function mockListApi(page: Page): Promise<Request[]> {
  const requests: Request[] = [];
  await mockSignedIn(page);
  await page.route(
    (url) => url.pathname === '/api/documents',
    (route) => {
      requests.push(route.request());
      const url = new URL(route.request().url());
      const title = normalize(url.searchParams.get('title') ?? '');
      const statuses = url.searchParams.getAll('status');
      const start = Number(url.searchParams.get('cursor') ?? 0);
      const matching = DOCUMENTS.filter(
        (document) =>
          normalize(document.title).includes(title) &&
          (statuses.length === 0 || statuses.includes(document.status)),
      );
      const items = matching.slice(start, start + PAGE_SIZE);
      const next = start + PAGE_SIZE < matching.length ? String(start + PAGE_SIZE) : null;
      return route.fulfill({ json: { items, nextCursor: next } });
    },
  );
  return requests;
}

const titleLinks = (page: Page) => page.locator('table a');

test('carga las páginas siguientes hasta que no quedan documentos', async ({ page }) => {
  await mockListApi(page);
  await page.goto('/documentos');

  await expect(titleLinks(page)).toHaveCount(20);
  // La tabla se desplaza dentro de su caja: la página nunca tiene scroll horizontal (WCAG 1.4.10).
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const loadMore = page.getByRole('button', { name: 'Cargar más documentos' });
  await loadMore.click();
  await expect(titleLinks(page)).toHaveCount(40);
  await expect(titleLinks(page).nth(20)).toBeFocused();

  await loadMore.click();
  await expect(titleLinks(page)).toHaveCount(45);
  await expect(loadMore).toHaveCount(0);
  await expect(
    page.getByRole('status').filter({ hasText: 'Se muestran 45 documentos.' }),
  ).toBeAttached();
});

test('busca por título sin tildes, filtra por estado y guarda la búsqueda en la URL', async ({
  page,
}) => {
  const requests = await mockListApi(page);
  await page.goto('/documentos');
  await expect(titleLinks(page)).toHaveCount(20);

  await page.getByRole('searchbox', { name: 'Buscar por título' }).fill('constitucion');
  await expect(titleLinks(page)).toHaveCount(15);
  await expect(titleLinks(page).first()).toContainText('Constitución Española');
  await expect(page).toHaveURL(/buscar=constitucion/);

  await page.getByRole('combobox', { name: 'Estado' }).click();
  await page.getByRole('option', { name: 'Con error' }).click();
  await page.keyboard.press('Escape');

  await expect(titleLinks(page)).toHaveCount(5);
  await expect(page).toHaveURL(/estado=Error/);
  const last = new URL(requests.at(-1)!.url());
  expect(last.searchParams.get('title')).toBe('constitucion');
  expect(last.searchParams.getAll('status')).toEqual(['Error']);

  // Al volver a la página con la URL, la búsqueda sigue ahí.
  await page.reload();
  await expect(page.getByRole('searchbox', { name: 'Buscar por título' })).toHaveValue(
    'constitucion',
  );
  await expect(titleLinks(page)).toHaveCount(5);
});

test('si nada coincide, lo dice y deja quitar los filtros', async ({ page }) => {
  await mockListApi(page);
  await page.goto('/documentos?buscar=inexistente');

  await expect(page.getByText('Ningún documento coincide con la búsqueda.')).toBeVisible();
  await expectNoAxeViolations(page);

  await page.getByRole('button', { name: 'Quitar filtros' }).first().click();
  await expect(titleLinks(page)).toHaveCount(20);
  await expect(page.getByRole('searchbox', { name: 'Buscar por título' })).toHaveValue('');
});

for (const colorScheme of ['light', 'dark'] as const) {
  test(`la lista con filtros pasa axe en tema ${colorScheme === 'light' ? 'claro' : 'oscuro'}`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme });
    await mockListApi(page);
    await page.goto('/documentos?estado=Draft');

    await expect(titleLinks(page)).toHaveCount(15);
    await expectNoAxeViolations(page);
  });
}
