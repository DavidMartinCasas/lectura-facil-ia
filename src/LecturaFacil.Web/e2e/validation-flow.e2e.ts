// SPDX-License-Identifier: Apache-2.0
// Validación humana: enviar un borrador revisado a validación → anotar lo que decidió el grupo → publicar. La API se
// simula con respuestas que cambian de estado como lo haría el backend. Cada paso pasa axe en tema claro y oscuro.
import { Page, expect, test } from '@playwright/test';
import {
  DocumentDetails,
  ValidationRecord,
  ValidationResult,
} from '../src/app/core/documents/document.models';
import { VALIDATION_TEXTS } from '../src/app/core/ui-texts';
import { adaptationFixture, documentFixture } from '../src/app/testing/document-fixtures';
import { expectNoAxeViolations } from './axe';
import { mockSignedIn } from './session';

const DOCUMENT_ID = '7f1c2a9e-0000-4000-8000-000000000021';

interface ApiCalls {
  sent: unknown[];
  recorded: ValidationResult[];
  published: number;
}

async function mockValidationApi(page: Page): Promise<ApiCalls> {
  const calls: ApiCalls = { sent: [], recorded: [], published: 0 };
  let current: DocumentDetails = documentFixture('Draft', {
    id: DOCUMENT_ID,
    canSendToValidation: true,
    adaptation: adaptationFixture({ pendingKeyFactCount: 0, canExport: true }),
  });
  const move = (changes: Partial<DocumentDetails>) => {
    current = { ...current, ...changes };
    return current;
  };
  await mockSignedIn(page);

  await page.route(`**/api/documents/${DOCUMENT_ID}`, (route) => route.fulfill({ json: current }));
  await page.route(`**/api/documents/${DOCUMENT_ID}/validation/send`, (route) => {
    calls.sent.push(route.request().postDataJSON());
    return route.fulfill({
      json: move({ status: 'PendingValidation', editable: false, canSendToValidation: false }),
    });
  });
  await page.route(`**/api/documents/${DOCUMENT_ID}/validation`, (route) => {
    const result = route.request().postDataJSON() as ValidationResult;
    calls.recorded.push(result);
    const validation: ValidationRecord = {
      version: 1,
      ...result,
      recordedAt: '2026-11-23T10:00:00Z',
    };
    return route.fulfill({ json: move({ status: 'Validated', validation }) });
  });
  await page.route(`**/api/documents/${DOCUMENT_ID}/publish`, (route) => {
    calls.published++;
    return route.fulfill({ json: move({ status: 'Published' }) });
  });
  return calls;
}

for (const colorScheme of ['light', 'dark'] as const) {
  test(`valida y publica un borrador revisado (tema ${colorScheme === 'light' ? 'claro' : 'oscuro'})`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme });
    const calls = await mockValidationApi(page);
    await page.goto(`/documentos/${DOCUMENT_ID}`);
    const panel = page.getByRole('region', { name: VALIDATION_TEXTS.title, exact: true });

    await expect(panel).toBeVisible();
    await expectNoAxeViolations(page);
    await panel.getByRole('button', { name: VALIDATION_TEXTS.send }).click();

    await expect(page.getByText('Pendiente de validación').first()).toBeVisible();
    await expect(panel.getByText(VALIDATION_TEXTS.pendingHint)).toBeVisible();
    expect(calls.sent).toEqual([{ version: 1 }]);

    // Sin elegir nada, el formulario dice qué falta.
    await panel.getByRole('button', { name: VALIDATION_TEXTS.record }).click();
    await expect(panel.getByText(VALIDATION_TEXTS.outcomeRequired)).toBeVisible();
    await expect(panel.getByText(VALIDATION_TEXTS.validatedByRequired)).toBeVisible();
    await expectNoAxeViolations(page);

    await panel.getByRole('radio', { name: VALIDATION_TEXTS.outcomes.Approved }).check();
    await panel
      .getByRole('textbox', { name: VALIDATION_TEXTS.validatedByLabel })
      .fill('Grupo de lectura');
    await panel
      .getByRole('textbox', { name: VALIDATION_TEXTS.notesLabel })
      .fill('Se entiende bien.');
    await panel.getByRole('button', { name: VALIDATION_TEXTS.record }).click();

    await expect(
      panel.getByText(VALIDATION_TEXTS.validated('23 de noviembre de 2026', 'Grupo de lectura')),
    ).toBeVisible();
    expect(calls.recorded).toEqual([
      { outcome: 'Approved', validatedBy: 'Grupo de lectura', notes: 'Se entiende bien.' },
    ]);
    await expectNoAxeViolations(page);

    await panel.getByRole('button', { name: VALIDATION_TEXTS.publish }).click();

    await expect(panel.getByText(VALIDATION_TEXTS.published)).toBeVisible();
    expect(calls.published).toBe(1);
    await expectNoAxeViolations(page);
  });
}
