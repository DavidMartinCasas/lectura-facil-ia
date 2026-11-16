// SPDX-License-Identifier: Apache-2.0
// Sesión simulada para los E2E de pantallas que exigen haber entrado (US-15). No hay backend: /api/auth/me responde
// como si ya hubiera una cookie de sesión válida.
import { Page } from '@playwright/test';

export const E2E_USER = 'persona.prueba@example.org';

export async function mockSignedIn(page: Page): Promise<void> {
  await page.route('**/api/auth/me', (route) => route.fulfill({ json: { email: E2E_USER } }));
}
