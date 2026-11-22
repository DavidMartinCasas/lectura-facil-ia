// SPDX-License-Identifier: Apache-2.0
// Auditoría automática de accesibilidad (WCAG 2.2 AA) con axe-core, que va dentro de node_modules: no se pide a
// ningún CDN.
import { Page, expect } from '@playwright/test';
import path from 'node:path';

const AXE_PATH = path.join(__dirname, '..', 'node_modules', 'axe-core', 'axe.min.js');

export async function expectNoAxeViolations(page: Page): Promise<void> {
  await page.addScriptTag({ path: AXE_PATH });
  const violations = await page.evaluate(async () => {
    const axe = (
      window as unknown as {
        axe: {
          run: (context: Document, options: object) => Promise<{ violations: { id: string }[] }>;
        };
      }
    ).axe;
    const result = await axe.run(document, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
    });
    return result.violations.map((violation) => violation.id);
  });
  expect(violations).toEqual([]);
}
