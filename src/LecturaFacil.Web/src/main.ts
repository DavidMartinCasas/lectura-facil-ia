// SPDX-License-Identifier: Apache-2.0
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { initMonitoring } from './app/core/monitoring/monitoring';

// Sentry se activa en paralelo, sin retrasar el arranque. Si no hay DSN, no hace nada; nunca falla.
void initMonitoring();

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
