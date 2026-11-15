# US-12 · Pantalla de subida y progreso

| Campo | Valor |
|---|---|
| **Épica** | EP-05 · Interfaz |
| **Prioridad** | Must |
| **Puntos** | 3 |
| **Día previsto** | Viernes 20 |
| **Depende de** | US-04, US-06 |
| **Requiere humano** | No |

## Historia
**Como** técnica de una administración
**quiero** pegar un texto o arrastrar un PDF y ver en todo momento en qué punto está el proceso
**para** saber que la herramienta está trabajando y cuándo puedo revisar el resultado.

## Contexto para el agente
- Endpoints: `POST /api/documents`, `GET /api/documents`, `GET /api/documents/{id}`, `GET /api/documents/{id}/events` (SSE).
- Accesibilidad: la propia web debe cumplir **WCAG 2.2 AA** (RNF-04). Una herramienta de accesibilidad cognitiva no puede ser inaccesible.

## Criterios de aceptación
```gherkin
Escenario: Subir un PDF
  Dado que estoy en la pantalla "Nuevo documento"
  Cuando arrastro un PDF válido y pulso "Adaptar"
  Entonces veo el progreso por pasos: Recibido, Adaptando, Revisando pautas, Verificando, Listo
  Y al terminar se abre la vista del documento

Escenario: Validación en el cliente
  Cuando intento subir un fichero de más de 5 MB o que no es PDF
  Entonces veo un mensaje claro junto al campo, sin llamar a la API

Escenario: Error del servidor
  Cuando la API devuelve un ProblemDetails
  Entonces veo un mensaje comprensible en español y un botón para reintentar

Escenario: Lista de documentos
  Cuando abro "Mis documentos"
  Entonces veo título, fecha, estado y proveedor que lo procesó

Escenario: Navegación con teclado
  Cuando uso solo el teclado
  Entonces puedo completar todo el flujo, con el foco siempre visible
```

## Alcance técnico
- **Angular:** rutas `/nuevo`, `/documentos`, `/documentos/:id` (esta última solo con el estado y un marcador de la US-13).
- `DocumentsApiService` (HTTP) y `DocumentEventsService` (SSE con `EventSource` y reconexión).
- Componentes *standalone* con `OnPush` y *signals*: `UploadFormComponent`, `ProgressStepsComponent`, `DocumentListComponent`.
- Componentes de PrimeNG (FileUpload, Steps o Timeline, Table) con tema claro y oscuro.
- Interceptor HTTP para errores que traduce ProblemDetails a mensajes de usuario.

## Seguridad
- **Sin `innerHTML`** ni `bypassSecurityTrust*`; todo el texto se pinta con interpolación.
- La validación del cliente es solo comodidad: la del servidor es la que manda.
- El interceptor **no muestra** detalles técnicos aunque lleguen en la respuesta.
- Cookies de sesión gestionadas por el navegador; el frontend no guarda tokens en `localStorage`.

## Calidad y clean code
- Componentes de presentación sin lógica de negocio; la lógica en servicios.
- Sin `any`; tipos generados o escritos a mano que coinciden con el OpenAPI.
- Textos de interfaz en un único sitio (constantes o i18n), en español claro.

## Tests requeridos
- [ ] Unitarios de los servicios (HTTP y SSE) con dobles.
- [ ] Unitarios de los componentes: estados vacío, cargando, error y completado.
- [ ] Test de accesibilidad automático (axe) de las tres pantallas.
- [ ] E2E con Playwright: subir texto → ver progreso → llegar a la vista del documento (con `FakeLlmProvider` en el backend).

## Fuera de alcance
- Vista lado a lado y edición (US-13).
- Login (US-15): de momento, usuario de desarrollo.

## Verificación
```bash
cd src/LecturaFacil.Web && npm run lint && npm test -- --watch=false && npm run build
npx playwright test
```

## Definition of Done específica
- [ ] Capturas de las pantallas en la PR (tema claro y oscuro, escritorio y móvil).
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-12-subida-y-progreso-ui.md.
Crea la rama feat/US-12-subida-ui, preséntame un plan breve y espera mi confirmación.
Comprueba la versión de Angular y PrimeNG del proyecto antes de usar sus APIs.
Implementa con tests, incluido el de accesibilidad, ejecuta todos los comandos de verificación y termina
con un resumen de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
