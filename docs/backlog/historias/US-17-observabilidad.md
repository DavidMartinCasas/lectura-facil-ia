# US-17 · Observabilidad sin datos personales

| Campo | Valor |
|---|---|
| **Épica** | EP-08 · Calidad y operación |
| **Prioridad** | Should |
| **Puntos** | 2 |
| **Día previsto** | Lunes 23 |
| **Depende de** | US-06 |
| **Requiere humano** | Sí: DSN de Sentry para backend y frontend |

## Historia
**Como** responsable del servicio
**quiero** ver errores, tiempos y qué proveedor procesa cada documento, sin exponer el contenido de nadie
**para** detectar problemas durante la evaluación del TFM y demostrar que el sistema se comporta como se diseñó.

## Contexto para el agente
- Curso, módulo 08: observabilidad con Sentry.
- Regla de `AGENTS.md`: **nunca registrar el contenido de los documentos** ni datos personales.

## Criterios de aceptación
```gherkin
Escenario: Error en el backend
  Cuando se produce una excepción no controlada en la API
  Entonces aparece en Sentry con la traza
  Y el evento no contiene el texto del documento, cookies, cabeceras de autenticación ni el correo del usuario

Escenario: Error en el frontend
  Cuando se produce un error de JavaScript
  Entonces aparece en Sentry con el entorno y la versión

Escenario: Trazas de adaptación
  Cuando se procesa un documento
  Entonces se registra un log estructurado con id del documento, proveedor (principal o respaldo), modelo, tokens, duración y resultado
  Y no se registra ningún fragmento del texto

Escenario: Métricas
  Cuando consulto /metrics desde la red interna
  Entonces veo contadores de adaptaciones por proveedor, errores y duración
```

## Alcance técnico
- **Serilog** con salida JSON, enriquecido con `TraceId` y `DocumentId`; nivel configurable.
- **Sentry** en ASP.NET Core y en Angular, con `SendDefaultPii = false` y un filtro `BeforeSend` que elimina cuerpos de petición, cookies, cabeceras sensibles y cualquier campo con nombre `text`, `content`, `original`, `adapted`.
- **Métricas** con `System.Diagnostics.Metrics` y exportación Prometheus en `/metrics`, **solo accesible desde la red interna o Tailscale** (no publicado por Cloudflare).
- Versión de la aplicación (`git sha`) en Sentry y en `/health`.

## Seguridad
- **Privacidad por diseño:** test que falla si un log o un evento de Sentry contiene el texto de un documento de prueba.
- `/metrics` no expuesto públicamente (regla en nginx o en el compose).
- DSN en el entorno, nunca en el repositorio (el DSN del frontend se inyecta en tiempo de despliegue).

## Calidad y clean code
- Un único sitio que define los campos de log de la adaptación (evita logs dispersos y distintos).
- Sin `Console.WriteLine` ni `console.log` en el código.

## Tests requeridos
- [ ] Test con sink de Serilog en memoria: al adaptar un documento con texto conocido, ningún evento contiene ese texto.
- [ ] Unitario del filtro `BeforeSend` de Sentry.
- [ ] Integración: `/metrics` responde dentro y está bloqueado en la configuración pública de nginx.

## Fuera de alcance
- Paneles de Grafana (los monta la persona en su homelab si le da tiempo).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Logging|FullyQualifiedName~Sentry|FullyQualifiedName~Metrics"
```

## Definition of Done específica
- [ ] Captura de un evento de Sentry de prueba sin datos sensibles en la PR.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-17-observabilidad.md.
Crea la rama feat/US-17-observabilidad, preséntame un plan breve y espera mi confirmación.
Implementa con tests primero, incluido el test de que ningún log contiene el texto del documento.
Ejecuta todos los comandos de verificación y termina con un resumen de lo hecho, cómo probarlo,
lo pendiente y los riesgos. No hagas push ni merge.
```
