// SPDX-License-Identifier: Apache-2.0
import type { Breadcrumb, BrowserOptions, ErrorEvent } from '@sentry/angular';
import { addIntegration, breadcrumbsIntegration, init } from '@sentry/angular';

type Integration = Parameters<typeof addIntegration>[0];

/** Configuración pública que da la API al cargar la página: el DSN se decide al desplegar, no al compilar (US-17). */
export interface MonitoringConfig {
  readonly dsn: string | null;
  readonly environment: string;
  readonly release: string;
}

export const MONITORING_CONFIG_URL = '/api/monitoring/config';

/** La API reenvía los informes a Sentry: el navegador solo habla con su origen y la CSP no cambia. */
export const MONITORING_TUNNEL_URL = '/api/monitoring/sentry';

export const REDACTED = '[filtrado]';

/** Partes de nombres de campo que pueden llevar texto de un documento o datos de la persona. */
const SENSITIVE_FIELD_HINTS = [
  'text',
  'content',
  'original',
  'adapted',
  'body',
  'email',
  'cookie',
  'authorization',
  'token',
  'password',
  'secret',
];

export function isSensitive(field: string): boolean {
  const name = field.toLowerCase();
  return SENSITIVE_FIELD_HINTS.some((hint) => name.includes(hint));
}

/** La URL sin consulta ni fragmento: la ruta basta para saber dónde falló. */
export function withoutQuery(url: string): string {
  return url.split(/[?#]/, 1)[0];
}

function redactFields<T>(
  record: Record<string, T> | undefined,
): Record<string, T | string> | undefined {
  if (!record) {
    return record;
  }

  return Object.fromEntries(
    Object.entries(record).map(([key, value]) => [key, isSensitive(key) ? REDACTED : value]),
  );
}

/**
 * Filtro `beforeSend`: nada del cuerpo, cookies, cabeceras, consulta de la URL ni datos de la persona, y ningún campo
 * cuyo nombre sugiera texto de un documento. La interfaz nunca pone texto en los errores; esto es la segunda barrera.
 */
export function scrubEvent(event: ErrorEvent): ErrorEvent {
  const request = event.request
    ? {
        method: event.request.method,
        url: event.request.url ? withoutQuery(event.request.url) : undefined,
      }
    : undefined;
  return {
    ...event,
    request,
    user: undefined,
    extra: redactFields(event.extra),
    contexts: event.contexts
      ? Object.fromEntries(Object.entries(event.contexts).filter(([key]) => !isSensitive(key)))
      : undefined,
    tags: event.tags
      ? Object.fromEntries(Object.entries(event.tags).filter(([key]) => !isSensitive(key)))
      : undefined,
  };
}

/** Filtro `beforeBreadcrumb`: sin consulta en las URLs y sin campos con nombre de texto. */
export function scrubBreadcrumb(breadcrumb: Breadcrumb): Breadcrumb {
  const data = redactFields(breadcrumb.data);
  if (data && typeof data['url'] === 'string') {
    data['url'] = withoutQuery(data['url']);
  }

  return { ...breadcrumb, data };
}

/** Sin migas de la consola ni de los clics: podrían llevar texto del documento o de la interfaz. */
export function withoutTextBreadcrumbs(defaults: Integration[]): Integration[] {
  return [
    ...defaults.filter(
      (integration) => integration.name !== 'Console' && integration.name !== 'Breadcrumbs',
    ),
    breadcrumbsIntegration({ dom: false }),
  ];
}

export function sentryOptions(config: MonitoringConfig & { readonly dsn: string }): BrowserOptions {
  return {
    dsn: config.dsn,
    environment: config.environment,
    release: config.release,
    tunnel: MONITORING_TUNNEL_URL,
    // Nada de la persona: ni usuario, ni cookies, ni cabeceras, ni cuerpos, ni la consulta de las URLs.
    dataCollection: {
      userInfo: false,
      cookies: false,
      httpHeaders: false,
      httpBodies: [],
      urlQueryParams: false,
    },
    tracesSampleRate: 0,
    integrations: withoutTextBreadcrumbs,
    beforeSend: scrubEvent,
    beforeBreadcrumb: scrubBreadcrumb,
  };
}

async function loadConfig(): Promise<MonitoringConfig> {
  const response = await fetch(MONITORING_CONFIG_URL, { credentials: 'same-origin' });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }

  return (await response.json()) as MonitoringConfig;
}

/**
 * Activa Sentry si la API da un DSN. Nunca falla: sin configuración, o si la API no responde, la aplicación arranca
 * igual y sin informes de errores.
 * @returns si Sentry ha quedado activo.
 */
export async function initMonitoring(
  load: () => Promise<MonitoringConfig> = loadConfig,
  start: (options: BrowserOptions) => unknown = init,
): Promise<boolean> {
  try {
    const config = await load();
    if (!config.dsn) {
      return false;
    }

    start(sentryOptions({ ...config, dsn: config.dsn }));
    return true;
  } catch {
    return false;
  }
}
