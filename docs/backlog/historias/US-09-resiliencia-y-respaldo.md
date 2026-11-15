# US-09 · Proveedores configurables con respaldo opcional

| Campo | Valor |
|---|---|
| **Épica** | EP-03 · Adaptación |
| **Prioridad** | Must |
| **Puntos** | 3 |
| **Día previsto** | Jueves 19 |
| **Depende de** | US-03 |
| **Requiere humano** | No (para la prueba real: parar llama-swap) |

## Historia
**Como** quien despliega la aplicación
**quiero** elegir por configuración un proveedor principal y, si quiero, uno de respaldo que entre solo cuando el principal falle
**para** decidir según mi infraestructura el equilibrio entre privacidad y disponibilidad, sin cambiar el código.

## Contexto para el agente
- La aplicación **funciona siempre igual**: no hay modos ni selección por parte del usuario.
- El proyecto es open source: la privacidad la decide **quien despliega** con la configuración, y es su responsabilidad. **Por defecto el respaldo está deshabilitado.**

| Configuración | Comportamiento |
|---|---|
| Solo principal local (respaldo deshabilitado) | Ningún documento sale de la infraestructura propia. Si el principal cae, los documentos esperan en cola |
| Principal local + respaldo Azure | Normalmente local; si el principal cae, responde Azure (Data Zone EU). **Es la configuración del despliegue del TFM** |
| Solo Azure | Todo se procesa en Azure |

- Diagrama de decisión: sección 17 de `docs/TFM-Lectura-Facil.md`.

## Criterios de aceptación
```gherkin
Escenario: Principal disponible
  Dado que el proveedor principal responde
  Cuando se adapta un documento
  Entonces lo procesa el principal

Escenario: Principal caído con respaldo habilitado
  Dado que el principal no responde o falla varias veces seguidas
  Y el respaldo está habilitado
  Cuando se adapta un documento
  Entonces lo procesa el respaldo
  Y la versión guardada indica qué proveedor la generó

Escenario: Principal caído sin respaldo
  Dado que el principal no responde
  Y el respaldo está deshabilitado
  Cuando se adapta un documento
  Entonces no se llama a ningún otro proveedor
  Y el documento queda en cola con el aviso "El servicio de IA no está disponible; se procesará cuando vuelva"

Escenario: Recuperación
  Dado que el circuito está abierto
  Cuando el principal vuelve a responder
  Entonces, pasado el tiempo de prueba, las peticiones vuelven al principal
  Y los documentos en cola se procesan

Escenario: Estado visible
  Cuando consulto GET /api/status/llm
  Entonces veo si el principal y el respaldo (si existe) están disponibles, sin URLs ni detalles internos
```

## Alcance técnico
- **Application:** `ResilientLlmProvider` (decorador de `ILlmProvider`) con la política:
  - principal → respaldo, solo si `Fallback.Enabled = true`;
  - si no hay respaldo, devuelve `Result` "ProviderUnavailable" y el documento vuelve a la cola.
- **Infrastructure:** pipeline de resiliencia con `Microsoft.Extensions.Http.Resilience` para el cliente del principal: timeout, reintento corto y **circuit breaker** (umbral y duración configurables).
- Health check del principal cada N segundos, expuesto en `/health` y en `GET /api/status/llm`.
- El worker reprocesa los documentos en cola cuando el health check vuelve a estar en verde.
- La interfaz muestra en cada documento quién lo procesó (por ejemplo, *"Servidor propio"* o *"Azure (UE)"*), con un nombre configurable por proveedor.

## Seguridad
- **Con el respaldo deshabilitado, nunca se le llama**: se comprueba en Application y tiene test propio. Es la garantía de privacidad para despliegues solo locales.
- `GET /api/status/llm` no expone URLs, IPs ni nombres de modelos: solo `disponible / no disponible`.
- Los fallos de proveedor no filtran mensajes internos al usuario.

## Calidad y clean code
- Patrón **decorador**: ni los casos de uso ni los adaptadores conocen la política de respaldo.
- Parámetros del circuit breaker en `Options`, no en el código.
- Sin `if` por tipo de proveedor fuera de la factoría de la US-03.

## Tests requeridos
- [ ] Unitarios de `ResilientLlmProvider` con dos `FakeLlmProvider`: principal OK; principal KO con respaldo; principal KO **sin respaldo** (el respaldo no se llama: verificarlo con NSubstitute).
- [ ] Test del circuit breaker con un `HttpMessageHandler` que falla N veces.
- [ ] Integración: sin respaldo, el documento queda en cola y se procesa cuando el principal vuelve.

## Fuera de alcance
- Más de dos proveedores o balanceo de carga entre ellos.

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Resilient|FullyQualifiedName~Circuit"
# Prueba manual (humano): parar llama-swap, adaptar un documento y comprobar que responde Azure.
```

## Definition of Done específica
- [ ] ADR `docs/adr/0003-proveedores-configurables.md` con la decisión: privacidad por configuración del despliegue.
- [ ] README: tabla con las tres configuraciones posibles y sus implicaciones de privacidad.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-09-resiliencia-y-respaldo.md.
Crea la rama feat/US-09-respaldo, preséntame un plan breve y espera mi confirmación.
Comprueba en la documentación oficial la API actual de Microsoft.Extensions.Http.Resilience antes de usarla.
Implementa con tests primero (incluido el test de que, con el respaldo deshabilitado, nunca se le llama),
ejecuta todos los comandos de verificación y termina con un resumen de lo hecho, cómo probarlo,
lo pendiente y los riesgos. No hagas push ni merge.
```
