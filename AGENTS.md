# AGENTS.md · lectura-facil-ia

> Instrucciones para **agentes de programación** (Claude Code, Codex, OpenCode, Copilot…). Este archivo va en la **raíz del repositorio**. Léelo entero antes de cualquier cambio.
> Si hay conflicto entre este archivo y una historia de usuario, **manda este archivo** en seguridad y arquitectura, y la historia en el alcance funcional.

---

## 1. Qué es el proyecto

Herramienta web que ayuda a adaptar textos administrativos a **lectura fácil** (norma UNE 153101 EX):

1. Recibe un texto o un PDF digital.
2. Detecta lo que dificulta la lectura (reglas deterministas).
3. Genera una adaptación con un LLM.
4. Revisa las pautas, mide la legibilidad (INFLESZ) y **verifica con un segundo LLM de otra familia** que no se pierde ni cambia ningún dato clave.
5. Muestra original y adaptación lado a lado, permite editar y exporta en **DOCX, PDF, Markdown o texto**.
6. Marca siempre el resultado como **borrador pendiente de validación humana**.

Diseño completo: `docs/TFM-Lectura-Facil.md`. Backlog: `docs/backlog/`.

### Es un proyecto open source y autoalojable
- Cualquiera puede instalarlo **en su propia infraestructura**, con **los proveedores de IA que elija**. Quien despliega es responsable de los datos que procesa.
- **El código no depende de ninguna infraestructura concreta.** Nada de IPs, dominios, nombres de servidores, Tailscale, OVH ni Cloudflare en el código. Esas cosas solo aparecen en `docs/despliegue.md` como **ejemplo** de despliegue.
- **Valores por defecto seguros:** sin configurar nada, la aplicación **no envía documentos a ningún proveedor que no se haya configurado**. El respaldo y Turnstile vienen **deshabilitados**.
- **Licencia del proyecto: Apache 2.0.** Todo fichero de código propio nuevo empieza con la cabecera `// SPDX-License-Identifier: Apache-2.0` (o el comentario equivalente del lenguaje).
- **Solo dependencias con licencias compatibles y permisivas** (MIT, Apache 2.0, BSD). Nada con licencias comerciales, *copyleft* (GPL, AGPL) o restricciones por facturación, porque afectarían a quien use el proyecto.

---

## 2. Stack

| Capa | Tecnología |
|---|---|
| Backend | **.NET 10 (LTS)**, ASP.NET Core Minimal APIs, C# con `nullable` activado |
| Frontend | **Angular** (versión estable actual) con componentes *standalone*, *signals* y TypeScript `strict`; **Angular Material** (MIT) |
| Base de datos | PostgreSQL + EF Core (Npgsql) |
| LLM | Puerto propio `ILlmProvider`; adaptadores con `Microsoft.Extensions.AI`: **`OpenAICompatible`** (Ollama, LM Studio, vLLM, llama-swap…) y **`AzureFoundry`**. Cualquiera puede ser principal o respaldo |
| Resiliencia | `Microsoft.Extensions.Http.Resilience` (Polly v8) |
| Lectura de PDF | **PdfPig** (Apache 2.0) |
| Exportación | **DOCX:** Open XML SDK (`DocumentFormat.OpenXml`, MIT). **PDF:** PDFsharp + MigraDoc (MIT). **Markdown y texto:** sin librerías |
| Validación | **FluentValidation** en el borde de la API |
| Tests | xUnit, **Shouldly** o **AwesomeAssertions**, NSubstitute, Testcontainers (PostgreSQL), Playwright |
| Observabilidad | Serilog (JSON) + Sentry |
| Contenedores | Docker y Docker Compose |

**Librerías prohibidas** (licencias comerciales o con restricciones para quien use el proyecto): **MediatR** (v13+), **AutoMapper** (v15+), **FluentAssertions** (v8+), **QuestPDF**, **PrimeNG** (v22+, licencia comercial PrimeUI). Los casos de uso son clases simples inyectadas y el mapeo se hace a mano.

**Nueva dependencia = justificación.** No añadas paquetes que no pida la historia. Si crees que hace falta uno, explícalo en el plan y espera confirmación.

---

## 3. Estructura

```text
lectura-facil-ia/
├── AGENTS.md
├── README.md
├── docs/ (diseño, backlog, adr/, evaluacion/)
├── src/
│   ├── LecturaFacil.Domain/          # Entidades, value objects, reglas. SIN dependencias externas
│   ├── LecturaFacil.Application/     # Casos de uso, puertos (interfaces), DTOs
│   ├── LecturaFacil.Infrastructure/  # Adaptadores: EF Core, LLM, PDF, legibilidad, exportación
│   ├── LecturaFacil.Api/             # Endpoints, autenticación, composición (DI)
│   └── LecturaFacil.Web/             # Angular
├── evals/                            # Proyecto de evaluación y datasets
├── tests/                            # Un proyecto de tests por capa + E2E
└── deploy/                           # docker-compose, .env.example, scripts
```

---

## 4. Reglas de arquitectura (Clean Architecture)

- **Regla de dependencias:** `Api → Application → Domain` e `Infrastructure → Application → Domain`. **Domain no referencia a nadie.** Application no referencia a Infrastructure ni a ASP.NET Core.
- **Puertos en Application, adaptadores en Infrastructure.** Ejemplos: `ILlmProvider`, `ITextExtractor`, `IGuidelineChecker`, `IMeaningVerifier`, `IReadabilityCalculator`, `IDocumentExporter`, `IDocumentRepository`.
- **Endpoints delgados:** validan la entrada, llaman a un caso de uso y devuelven el resultado. **Nada de lógica de negocio en `Api`.**
- **Las reglas de lectura fácil viven en Domain** y son código determinista y testeable, nunca prompts.
- **Los prompts** viven en `Infrastructure/Llm/Prompts/*.md`, versionados y cargados como recursos. Nunca en strings sueltos por el código.
- Un **test de arquitectura** (NetArchTest) comprueba la regla de dependencias. No lo desactives.

---

## 5. Clean code

- **SOLID, DRY, KISS y YAGNI.** Si dudas entre algo genérico y algo simple que resuelve la historia, elige lo simple.
- **Funciones cortas** con un único nivel de abstracción; **cláusulas de guarda** en lugar de anidar.
- **Nombres que expliquen la intención.** El código y los identificadores, en **inglés**; los textos de interfaz y los mensajes al usuario, en **español**.
- **Sin números mágicos:** umbrales (palabras por frase, tamaños máximos, tokens) en `Options` configurables.
- **Errores esperados** (validación, documento no encontrado, proveedor no disponible) con un tipo `Result`; **excepciones solo para lo excepcional**.
- **Inmutabilidad por defecto:** `record` para DTOs y value objects; colecciones de solo lectura en las salidas.
- **`async` hasta el final**, con `CancellationToken` en toda la cadena. Nunca `.Result` ni `.Wait()`.
- **Sin código comentado** ni `TODO` sin referencia a una historia (`// TODO(US-14): …`).
- **C#:** namespaces de ámbito de archivo, `TreatWarningsAsErrors`, analizadores de .NET activados, `dotnet format` sin cambios.
- **Angular:** componentes *standalone* con `OnPush`, *signals* para el estado, servicios para HTTP, **sin `any`**, ESLint sin errores.
- **Antipatrones a evitar:** clases dios, servicios "Helper/Utils/Manager" genéricos, lógica duplicada entre front y back, *primitive obsession* (usa value objects para importes, plazos, IDs).

---

## 6. Seguridad (obligatorio en cada historia)

Referencias: OWASP Top 10, **OWASP API Security Top 10 (2023)** y **OWASP Top 10 para aplicaciones LLM**. Principios: **defensa en profundidad, mínimo privilegio, seguro por defecto y separación de responsabilidades**.

### Secretos y configuración
- **Nunca** escribas secretos en código, tests, ejemplos, logs ni commits. Usa variables de entorno y `deploy/.env.example` **sin valores reales**.
- Toda configuración se carga en `Options` con **validación al arrancar** (`ValidateDataAnnotations` + `ValidateOnStart`): si falta algo, la aplicación **no arranca**.
- `gitleaks` se ejecuta en CI. Si detecta algo, **para y avisa**; no lo silencies.

### Entrada
- **Valida todo en el borde** con FluentValidation: longitudes, formatos, rangos.
- **Ficheros:** tamaño máximo configurable, comprobación del tipo **por su firma (magic bytes)**, no por la extensión; nunca uses el nombre de fichero del usuario para guardar; guarda fuera de cualquier ruta pública.
- **Base de datos:** solo EF Core con consultas parametrizadas. **Prohibido** `FromSqlRaw` con concatenación.

### Salida y errores
- **Nunca** renderices texto del LLM o del usuario como HTML. En Angular, **prohibido** `innerHTML` con contenido dinámico y `bypassSecurityTrust*`.
- **Exportaciones:** el texto se escapa según el formato. En Markdown se neutralizan enlaces, imágenes y HTML (un `![](https://…)` generado por el LLM podría filtrar datos al abrirse en otro visor).
- Errores con **ProblemDetails** genéricos: sin trazas, sin rutas internas, sin nombres de modelos ni de servidores.
- Cabeceras de seguridad: `Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Permissions-Policy`.

### Autenticación y autorización
- Cookies `HttpOnly`, `Secure` y `SameSite=Strict`; protección antiforgery en las peticiones que cambian estado.
- **Autorización por objeto** en cada acceso a un documento (OWASP API1 · BOLA): un usuario solo ve los suyos.
- Límites de peticiones (`RateLimiter` de ASP.NET Core) en todos los endpoints que llaman a un LLM.

### Integración con LLM
- El texto del usuario es **dato no confiable**: va siempre **entre delimitadores** y el prompt de sistema indica que no contiene instrucciones (OWASP LLM01).
- El LLM **no tiene herramientas** ni acceso a nada más que el texto que recibe.
- La respuesta se pide en **JSON con esquema** y **se valida** antes de usarse; si no valida, se trata como error.
- `max_tokens` y tamaño de entrada siempre acotados (OWASP LLM10).
- **Nunca** metas secretos, rutas internas ni datos de otros usuarios en un prompt.
- **Privacidad por configuración:** la aplicación funciona siempre igual; quien la despliega decide los proveedores. El **proveedor de respaldo solo se usa si está habilitado** en la configuración: si no lo está, ningún documento sale del proveedor principal. Esta regla tiene test propio.

### Registro y privacidad
- **Nunca registres el contenido de los documentos** ni datos personales. Registra IDs, tamaños, tiempos, modelo y proveedor.
- Sentry con `SendDefaultPii = false` y depuración de datos antes de enviar.

### Dependencias y contenedores
- Versiones fijadas. `dotnet list package --vulnerable` y `npm audit --omit=dev` sin vulnerabilidades altas o críticas.
- Imágenes Docker mínimas, **usuario no root**, sin secretos en la imagen.

---

## 7. Tests

- **TDD en Domain y Application:** primero el test que falla, luego el código.
- Cobertura mínima de **80 % en Domain y Application**. No bajes el umbral.
- **Tests de integración** con Testcontainers para PostgreSQL y con un **proveedor LLM falso** (`FakeLlmProvider`) que devuelve respuestas grabadas. **Los tests nunca llaman a un LLM real.**
- **Tests de seguridad** cuando la historia lo pida: acceso a documento ajeno → 404, fichero con firma falsa → 400, con el respaldo deshabilitado nunca se llama al proveedor de respaldo, etc.
- Nombres de test que describan el comportamiento: `Adapt_WhenProviderReturnsInvalidJson_ReturnsProviderError`.
- **Prohibido** desactivar, saltar o borrar tests para que el build pase.

---

## 8. Comandos de verificación

Ejecútalos todos antes de dar una historia por terminada:

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
cd src/LecturaFacil.Web && npm ci && npm run lint && npm test -- --watch=false && npm run build
gitleaks detect --no-banner
docker compose -f deploy/docker-compose.yml config --quiet
```

---

## 9. Flujo de trabajo por historia

1. **Lee** este archivo y la historia (`docs/backlog/historias/US-XX-*.md`) completa.
2. **Crea la rama** `feat/US-XX-slug-corto`.
3. **Presenta un plan breve** (archivos que vas a crear o tocar, tests, riesgos):
   - **En una sesión interactiva:** espera confirmación antes de escribir código.
   - **En un agente en la nube sin nadie al otro lado:** escribe el plan al principio de la descripción de la PR y continúa. Si la historia te obliga a tomar una decisión de diseño que no está en el backlog, elige la opción más simple y segura y **explícala en la PR** para que la persona la revise.
4. **Implementa en pasos pequeños**, con tests primero en Domain y Application.
5. **Ejecuta los comandos de verificación** y corrige hasta que pasen.
6. **Actualiza la documentación** que la historia indique.
7. **Commits** con Conventional Commits y la referencia de la historia: `feat(adaptation): validate LLM JSON output (US-06)`.
8. **Entrega un resumen** (en la PR si trabajas en la nube): qué has hecho, cómo se prueba, qué queda pendiente y riesgos.

> En la nube **no tienes secretos ni acceso a los modelos reales**, y no debes pedirlos: todo se prueba con `FakeLlmProvider`. Si algo solo puede comprobarse con un modelo real, déjalo indicado en la PR como prueba manual para la persona.

### Nunca hagas esto
- Push o merge a `main`, *force push*, reescribir historia publicada.
- Tocar archivos fuera del alcance de la historia sin avisar.
- Añadir dependencias, cambiar umbrales o desactivar reglas de seguridad sin confirmación.
- Usar datos personales reales en tests, ejemplos o seeds.
- Ejecutar comandos destructivos (`rm -rf`, `docker system prune`, `DROP`…) o cualquier cosa contra servidores reales.
- Inventar APIs: si no estás seguro de una firma o un paquete, **compruébalo** en la documentación o compilando.

### Si te bloqueas
Para, explica qué falta (una clave, una decisión de diseño, una URL) y propón opciones. No improvises una solución insegura para seguir avanzando.

---

## 10. Skills y revisores

Recetas y listas de comprobación para tareas que se repiten. Están en formato de Claude Code (`.claude/skills/` y `.claude/agents/`). **Si tu herramienta no las carga sola, abre el fichero y síguelo** cuando la tarea encaje.

| Tipo | Nombre | Cuándo usarlo |
|---|---|---|
| Skill | [`implementar-historia`](.claude/skills/implementar-historia/SKILL.md) | Al implementar cualquier US: orden de los pasos y plantilla de PR |
| Skill | [`verificar`](.claude/skills/verificar/SKILL.md) | Antes de abrir o actualizar una PR |
| Skill | [`nuevo-endpoint`](.claude/skills/nuevo-endpoint/SKILL.md) | Al crear o cambiar un endpoint de la API |
| Skill | [`prompt-llm`](.claude/skills/prompt-llm/SKILL.md) | Al tocar prompts, `ILlmProvider` o respuestas de modelos |
| Revisor | [`revisor-seguridad`](.claude/agents/revisor-seguridad.md) | Antes de cada PR: OWASP Top 10, API Top 10 y LLM Top 10 |
| Revisor | [`revisor-arquitectura`](.claude/agents/revisor-arquitectura.md) | Antes de cada PR: capas, clean code, tests y licencias |
| Revisor | [`experto-lectura-facil`](.claude/agents/experto-lectura-facil.md) | En US-05, 06, 07, 08, 11 y 18: fidelidad a la UNE 153101 EX |

Este archivo sigue mandando: las skills y los revisores lo concretan, no lo cambian.

---

## Code Review Rules

Reglas para los revisores automáticos de PRs (Codex en GitHub y cualquier otro).

**Idioma: escribe siempre la revisión, los comentarios y los resúmenes en español de España.** Deja en inglés solo el código, los identificadores y los mensajes de error literales.

Comenta solo lo que rompa una de estas reglas o cause un fallo concreto; cita fichero y línea y propone la corrección. Las secciones 4 a 7 de este archivo son la referencia completa.

### Seguridad y privacidad (siempre P0/P1)
- Acceso a un documento sin filtrar por propietario, o que devuelve 403 en lugar de 404 (BOLA).
- Endpoint que llama a un LLM sin autorización o sin límite de peticiones; entradas, ficheros o `max_tokens` sin límite.
- Texto del usuario en un prompt sin delimitadores, o respuesta del LLM usada sin validarla contra su esquema JSON.
- Cualquier camino que llame al proveedor de respaldo con `Fallback.Enabled = false`.
- Contenido de documentos, correos o datos personales en logs, excepciones, ProblemDetails o Sentry.
- Secretos, cadenas de conexión, IPs, dominios o infraestructura concreta en el código, tests o configuración de ejemplo.
- `innerHTML` o `bypassSecurityTrust*` con contenido dinámico; Markdown, DOCX o PDF exportados sin escapar el texto.
- SQL con `FromSqlRaw`/`ExecuteSqlRaw` concatenado; tipo de fichero comprobado por extensión en vez de por firma.

### Producto
- El resultado deja de marcarse como borrador pendiente de validación humana.
- El verificador del significado puede quitar avisos que detecta el código (solo puede añadirlos).
- Reglas de lectura fácil implementadas como prompt en lugar de código determinista en Domain.

### Arquitectura, dependencias y tests
- Domain o Application referenciando Infrastructure, ASP.NET Core o EF Core; lógica de negocio en endpoints.
- MediatR, AutoMapper, FluentAssertions, QuestPDF o cualquier dependencia GPL, AGPL o comercial; versiones sin fijar.
- Tests desactivados, saltados o borrados, umbrales de cobertura rebajados o tests que llaman a un LLM real.
- Cambios en `.github/workflows` que usen acciones sin fijar por SHA, `pull_request_target` con el código de la PR o permisos amplios.

### No comentes
- Formato, estilo, orden de `using` o nombres: lo cubren `dotnet format` y ESLint.
- Preferencias de diseño que no contradigan una regla de este archivo.
