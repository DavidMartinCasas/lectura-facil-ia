// SPDX-License-Identifier: Apache-2.0
// E2E de la interfaz. Arranca `ng serve` y simula la API en el navegador (page.route): no necesita backend ni LLM.
import { defineConfig, devices } from '@playwright/test';

const port = 4300;
// Navegador ya instalado (por ejemplo, en un contenedor sin descargas). Si no se indica, usa el de Playwright.
const executablePath = process.env['E2E_CHROMIUM_PATH'] || undefined;

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: 'list',
  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'off',
    launchOptions: { executablePath },
  },
  projects: [
    {
      name: 'escritorio',
      use: { ...devices['Desktop Chrome'], launchOptions: { executablePath } },
    },
    { name: 'movil', use: { ...devices['Pixel 7'], launchOptions: { executablePath } } },
  ],
  webServer: {
    command: `npx ng serve --port ${port}`,
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env['CI'],
    timeout: 120_000,
  },
});
