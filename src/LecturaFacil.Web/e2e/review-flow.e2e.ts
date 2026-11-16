// SPDX-License-Identifier: Apache-2.0
// Flujo de la US-13: abrir un borrador → ver el aviso → marcarlo como revisado → editar → ver la legibilidad
// recalculada. La API se simula con respuestas fijas que cambian como lo haría el backend (sin LLM: el recálculo lo
// hace el servidor con código).
import { Page, expect, test } from '@playwright/test';
import { DocumentDetails, ManualEdit } from '../src/app/core/documents/document.models';
import { adaptationFixture, documentFixture } from '../src/app/testing/document-fixtures';
import { mockSignedIn } from './session';

const DOCUMENT_ID = '7f1c2a9e-0000-4000-8000-000000000013';
const EDITED_SENTENCE = 'Le damos dinero para pagar el alquiler.';

interface ApiCalls {
  acknowledged: { factId: string; body: unknown }[];
  edits: { version: string; body: ManualEdit }[];
}

async function mockReviewApi(page: Page, initial: DocumentDetails): Promise<ApiCalls> {
  const calls: ApiCalls = { acknowledged: [], edits: [] };
  let current = initial;
  await mockSignedIn(page);

  await page.route(`**/api/documents/${DOCUMENT_ID}`, (route) => route.fulfill({ json: current }));

  await page.route(`**/api/documents/${DOCUMENT_ID}/key-facts/*/acknowledge`, (route) => {
    const factId = route.request().url().split('/key-facts/')[1].split('/')[0];
    calls.acknowledged.push({ factId, body: route.request().postDataJSON() });
    const adaptation = current.adaptation!;
    current = {
      ...current,
      adaptation: {
        ...adaptation,
        pendingKeyFactCount: 0,
        canExport: true,
        verification: {
          ...adaptation.verification!,
          keyFacts: adaptation.verification!.keyFacts.map((fact) =>
            String(fact.id) === factId
              ? { ...fact, acknowledged: true, requiresAcknowledgement: false }
              : fact,
          ),
        },
      },
    };
    return route.fulfill({ json: current });
  });

  await page.route(`**/api/documents/${DOCUMENT_ID}/versions/*`, (route) => {
    const version = route.request().url().split('/versions/')[1];
    const body = route.request().postDataJSON() as ManualEdit;
    calls.edits.push({ version, body });
    current = {
      ...current,
      adaptation: {
        ...current.adaptation!,
        version: 2,
        promptVersion: 'manual',
        sections: body.sections,
      },
      readability: {
        original: current.readability!.original,
        adapted: { value: 84.6, grade: 'VeryEasy', gradeLabel: 'Muy fácil' },
        improvement: 46.2,
      },
    };
    return route.fulfill({ json: current });
  });

  return calls;
}

test('revisa un borrador: aviso, marcar revisado, editar y legibilidad recalculada', async ({
  page,
  isMobile,
}) => {
  const calls = await mockReviewApi(page, documentFixture('Draft', { id: DOCUMENT_ID }));
  await page.goto(`/documentos/${DOCUMENT_ID}`);

  const banner = page.getByRole('region', { name: 'Borrador · pendiente de validación' });
  await expect(banner).toContainText('Servidor propio');
  await expect(banner).toContainText('marca como revisado el dato clave con aviso');

  const warning = page.locator('app-key-facts-panel .item--warning');
  await expect(warning).toContainText('40 % de la renta mensual');
  await expect(warning).toContainText('No aparece en la adaptación');
  await page.getByRole('button', { name: /Marcar como revisado/ }).click();

  await expect(page.locator('app-key-facts-panel')).toContainText('Revisado');
  await expect(banner).toContainText('Todos los avisos de datos clave están revisados.');
  expect(calls.acknowledged).toEqual([{ factId: '0', body: { version: 1 } }]);

  if (isMobile) {
    await page.getByRole('tab', { name: 'Adaptación' }).click();
  }
  await page.getByRole('button', { name: /Editar sección/ }).click();
  await page
    .getByLabel('Frases (una por línea)')
    .fill(`${EDITED_SENTENCE}\nComo mucho, 250 euros al mes.`);
  await page.getByRole('button', { name: 'Guardar' }).click();

  await expect(banner).toContainText('Versión 2');
  await expect(page.getByText(EDITED_SENTENCE)).toBeVisible();
  const readability = page.locator('app-readability-panel');
  await expect(readability).toContainText('85');
  await expect(readability).toContainText('Muy fácil');
  expect(calls.edits).toHaveLength(1);
  expect(calls.edits[0].version).toBe('1');
  expect(calls.edits[0].body.sections[0].sentences).toEqual([
    EDITED_SENTENCE,
    'Como mucho, 250 euros al mes.',
  ]);
});

test('explica la pauta de un fragmento marcado del original, también con el teclado', async ({
  page,
  isMobile,
}) => {
  test.skip(isMobile, 'En móvil no hay teclado físico.');
  await mockReviewApi(page, documentFixture('Draft', { id: DOCUMENT_ID }));
  await page.goto(`/documentos/${DOCUMENT_ID}`);

  const fragment = page.getByRole('button', { name: /^40 %/ });
  await fragment.focus();
  await page.keyboard.press('Enter');

  await expect(fragment).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('app-original-text .detail')).toContainText('Sin porcentajes');
  await expect(page.locator('app-original-text .detail')).toContainText(
    'Explica la cantidad sin porcentajes.',
  );
});

test('en móvil, original y adaptación van en pestañas', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'Solo en pantallas pequeñas.');
  await mockReviewApi(page, documentFixture('Draft', { id: DOCUMENT_ID }));
  await page.goto(`/documentos/${DOCUMENT_ID}`);

  await expect(page.getByRole('tab', { name: 'Texto original' })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await page.getByRole('tab', { name: 'Adaptación' }).click();
  await expect(page.getByRole('heading', { name: 'Ayudas para el alquiler' })).toBeVisible();
});

test('muestra como texto literal lo que parece HTML', async ({ page }) => {
  const dangerous = '<img src=x onerror="window.__xss=1">';
  await mockReviewApi(
    page,
    documentFixture('Draft', {
      id: DOCUMENT_ID,
      original: { text: `Texto original ${dangerous} con etiquetas.`, findings: [] },
      adaptation: adaptationFixture({
        sections: [{ question: '¿Qué pasa?', sentences: [`Frase ${dangerous}.`] }],
      }),
    }),
  );
  await page.goto(`/documentos/${DOCUMENT_ID}`);

  await expect(page.locator('app-original-text')).toContainText(dangerous);
  await expect(page.locator('app-document-review img')).toHaveCount(0);
  expect(
    await page.evaluate(() => (window as unknown as { __xss?: number }).__xss),
  ).toBeUndefined();
});
