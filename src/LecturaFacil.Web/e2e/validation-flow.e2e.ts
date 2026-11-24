// SPDX-License-Identifier: Apache-2.0
// Validación humana: enviar un borrador revisado a validación → anotar lo que decidió el grupo → publicar, o volver a
// borrador con las partes difíciles marcadas. La API se simula con respuestas que cambian de estado como lo haría el
// backend. Cada paso pasa axe en tema claro y oscuro.
import { Page, expect, test } from '@playwright/test';
import {
  DocumentDetails,
  ValidationRecord,
  ValidationResult,
} from '../src/app/core/documents/document.models';
import { REVIEW_TEXTS, VALIDATION_TEXTS } from '../src/app/core/ui-texts';
import { adaptationFixture, documentFixture } from '../src/app/testing/document-fixtures';
import { expectNoAxeViolations } from './axe';
import { mockSignedIn } from './session';

const DOCUMENT_ID = '7f1c2a9e-0000-4000-8000-000000000021';

type RecordedResult = ValidationResult & { version: number };

interface ApiCalls {
  sent: unknown[];
  recorded: RecordedResult[];
  published: number;
}

async function mockValidationApi(page: Page): Promise<ApiCalls> {
  const calls: ApiCalls = { sent: [], recorded: [], published: 0 };
  const adaptation = adaptationFixture({ pendingKeyFactCount: 0, canExport: true });
  let current: DocumentDetails = documentFixture('Draft', {
    id: DOCUMENT_ID,
    canSendToValidation: true,
    adaptation,
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
    const result = route.request().postDataJSON() as RecordedResult;
    calls.recorded.push(result);
    const validation: ValidationRecord = {
      round: 1,
      version: result.version,
      outcome: result.outcome,
      validatedBy: result.validatedBy,
      participants: result.participants,
      hardSections: result.hardSections.map((index) => adaptation.sections[index].question),
      hardGlossary: result.hardGlossary,
      notes: result.notes,
      recordedAt: '2026-11-23T10:00:00Z',
    };
    const approved = result.outcome === 'Approved';
    return route.fulfill({
      json: move({
        status: approved ? 'Validated' : 'Draft',
        editable: !approved,
        validation,
        validations: [validation],
      }),
    });
  });
  await page.route(`**/api/documents/${DOCUMENT_ID}/publish`, (route) => {
    calls.published++;
    return route.fulfill({ json: move({ status: 'Published' }) });
  });
  return calls;
}

for (const colorScheme of ['light', 'dark'] as const) {
  const theme = colorScheme === 'light' ? 'claro' : 'oscuro';

  test(`valida y publica un borrador revisado (tema ${theme})`, async ({ page }) => {
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
    await expect(panel.getByText(VALIDATION_TEXTS.participantsRequired)).toBeVisible();
    await expectNoAxeViolations(page);

    await panel.getByRole('radio', { name: VALIDATION_TEXTS.outcomes.Approved }).check();
    await panel
      .getByRole('textbox', { name: VALIDATION_TEXTS.validatedByLabel })
      .fill('Grupo de lectura');
    await panel.getByRole('spinbutton', { name: VALIDATION_TEXTS.participantsLabel }).fill('5');
    await panel
      .getByRole('textbox', { name: VALIDATION_TEXTS.notesLabel })
      .fill('Se entiende bien.');
    await panel.getByRole('button', { name: VALIDATION_TEXTS.record }).click();

    await expect(
      panel.getByText(
        VALIDATION_TEXTS.validated('23 de noviembre de 2026', 'Grupo de lectura (5 personas)'),
      ),
    ).toBeVisible();
    expect(calls.recorded).toEqual([
      {
        version: 1,
        outcome: 'Approved',
        validatedBy: 'Grupo de lectura',
        participants: 5,
        hardSections: [],
        hardGlossary: false,
        notes: 'Se entiende bien.',
      },
    ]);
    await expectNoAxeViolations(page);

    await panel.getByRole('button', { name: VALIDATION_TEXTS.publish }).click();

    await expect(panel.getByText(VALIDATION_TEXTS.published)).toBeVisible();
    expect(calls.published).toBe(1);
    await expectNoAxeViolations(page);
  });

  test(`si el grupo pide cambios, marca lo que costó entender y no deja reenviarlo igual (tema ${theme})`, async ({
    page,
  }) => {
    await page.emulateMedia({ colorScheme });
    const calls = await mockValidationApi(page);
    await page.goto(`/documentos/${DOCUMENT_ID}`);
    const panel = page.getByRole('region', { name: VALIDATION_TEXTS.title, exact: true });
    await panel.getByRole('button', { name: VALIDATION_TEXTS.send }).click();
    await expect(panel.getByText(VALIDATION_TEXTS.pendingHint)).toBeVisible();

    await panel.getByRole('radio', { name: VALIDATION_TEXTS.outcomes.ChangesRequested }).check();
    const hardParts = panel.getByRole('group', { name: VALIDATION_TEXTS.hardPartsLegend });
    await expect(hardParts).toBeVisible();
    await expectNoAxeViolations(page);
    await hardParts.getByRole('checkbox', { name: '¿Cuánto dinero dan?' }).check();
    await panel.getByRole('textbox', { name: VALIDATION_TEXTS.validatedByLabel }).fill('Grupo');
    await panel.getByRole('spinbutton', { name: VALIDATION_TEXTS.participantsLabel }).fill('4');
    await panel.getByRole('button', { name: VALIDATION_TEXTS.record }).click();

    await expect(panel.getByText(VALIDATION_TEXTS.hardPartsTitle)).toBeVisible();
    expect(calls.recorded[0]).toMatchObject({ hardSections: [0], hardGlossary: false });
    // En pantallas estrechas el original y la adaptación van en pestañas.
    const adaptedTab = page.getByRole('tab', { name: REVIEW_TEXTS.adaptedTitle });
    if (await adaptedTab.isVisible()) {
      await adaptedTab.click();
    }
    const hardSection = page.locator('.section--hard').filter({ hasText: '¿Cuánto dinero dan?' });
    await expect(hardSection.first()).toBeVisible();
    await expect(hardSection.first().getByText(VALIDATION_TEXTS.hardMark)).toBeVisible();
    await expect(panel.getByRole('button', { name: VALIDATION_TEXTS.send })).toBeDisabled();
    await expect(panel.getByText(VALIDATION_TEXTS.sendUnchanged)).toBeVisible();
    await expectNoAxeViolations(page);
  });
}
