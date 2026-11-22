# Lectura Fácil IA

Asistente **open source y autoalojable** que adapta textos administrativos a **lectura fácil** (norma UNE 153101 EX) con modelos de IA locales o en la nube. Genera **borradores** que siempre debe validar una persona.

> 🚧 **En desarrollo.** Proyecto Final del Máster en IA de BigSchool (noviembre de 2026). El README completo llegará con la [US-20](docs/backlog/historias/US-20-readme-y-adrs.md).

## Cómo arrancar en local

### Con Docker (todo junto)

Necesitas Docker con Docker Compose.

```bash
cp deploy/.env.example deploy/.env   # y cambia POSTGRES_PASSWORD y las opciones LLM__PRIMARY__*
docker compose -f deploy/docker-compose.yml up --build
```

- Aplicación: <http://localhost:8080>
- Estado de la API: `curl http://localhost:8080/health` → `{"status":"Healthy"}`

Solo se publica el frontend, y solo en `127.0.0.1`. nginx reenvía `/api` y `/health` a la API, y PostgreSQL no expone su puerto.

### Para desarrollar

Necesitas el SDK de .NET 10 (la versión está en `global.json`), Node 26 (`.nvmrc`) y Docker para los tests de integración.

```bash
# Backend
dotnet build -warnaserror
dotnet test            # incluye Testcontainers (PostgreSQL) y la cobertura mínima del 80 % en Domain y Application

# Frontend
cd src/LecturaFacil.Web
npm ci
npm start              # http://localhost:4200; reenvía /api a la API en http://localhost:5125 (proxy.conf.json)
npm run lint && npm test -- --watch=false && npm run build
```

Para ejecutar la API fuera de Docker, define las opciones obligatorias de la [configuración](#configuración) como variables de entorno o con `dotnet user-secrets` (solo en `Development`); nunca las escribas en un `appsettings` del repositorio. El documento OpenAPI solo se publica en `Development`, en `/openapi/v1.json`.

```bash
cd src/LecturaFacil.Api
dotnet user-secrets set "Database:ConnectionString" "Host=localhost;Database=lecturafacil;Username=lecturafacil"
dotnet user-secrets set "Database:Password" "<tu contraseña>"
dotnet user-secrets set "Llm:Primary:ApiKey" "<tu clave>"
```

## Configuración

Toda la configuración llega por variables de entorno (en Docker, desde `deploy/.env`; la plantilla comentada es [`deploy/.env.example`](deploy/.env.example)). Cada opción tiene su clase `Options` tipada y **se valida al arrancar**: si falta una obligatoria o un valor no es válido, la API **no arranca** y el log indica qué opción falla, **nunca su valor**.

El doble guion bajo separa niveles y no importan las mayúsculas: `LLM__PRIMARY__APIKEY` es la opción `Llm:Primary:ApiKey`. 🔒 = secreto: nunca se registra ni se muestra.

| Variable | Obligatoria | Por defecto | Descripción |
|---|---|---|---|
| `DATABASE__CONNECTIONSTRING` 🔒 | Sí | — | Cadena de conexión de PostgreSQL; debe indicar `Host`. En Docker la pone `docker-compose.yml`. |
| `DATABASE__NAME`, `DATABASE__USERNAME` | No | — | Base de datos y usuario, aparte de la cadena (se escapan solos). |
| `DATABASE__PASSWORD` 🔒 | No | — | Contraseña, aparte de la cadena. |
| `DATABASE__APPLYMIGRATIONSONSTARTUP` | No | `false` | Aplica las migraciones al arrancar. |
| `LLM__PRIMARY__TYPE` | Sí | — | `OpenAICompatible` (Ollama, LM Studio, vLLM, llama-swap…) o `AzureFoundry`. |
| `LLM__PRIMARY__DISPLAYNAME` | Sí | — | Nombre visible del proveedor (máx. 60 caracteres). |
| `LLM__PRIMARY__ENDPOINT` | Sí | — | URL absoluta `https`, sin credenciales. Se admite `http` solo hacia `localhost` o una IP privada, nunca hacia un nombre (el DNS podría resolverlo a una IP pública). |
| `LLM__PRIMARY__APIKEY` 🔒 | Sí | — | Clave del proveedor. |
| `LLM__PRIMARY__ADAPTERMODEL` | Sí | — | Modelo o despliegue que adapta el texto. |
| `LLM__PRIMARY__VERIFIERMODEL` | Sí | — | Modelo o despliegue que verifica el significado. |
| `LLM__PRIMARY__TEMPERATURE` | No | `0` | Temperatura de las respuestas (0 – 2). Súbela solo si tu modelo no admite 0. |
| `LLM__FALLBACK__ENABLED` | No | `false` | Habilita el proveedor de respaldo. Si es `false`, ningún documento sale del principal. |
| `LLM__FALLBACK__*` | Solo si está habilitado | — | Las mismas opciones que `LLM__PRIMARY__*` (la clave 🔒). |
| `LIMITS__MAXFILESIZEBYTES` | No | `5242880` (5 MB) | Tamaño máximo de fichero (1 KB – 50 MB). |
| `LIMITS__MAXCHARACTERS` | No | `20000` | Caracteres máximos del texto (100 – 200 000). |
| `LIMITS__MINCHARACTERS` | No | `50` | Caracteres mínimos del texto (1 – 100). |
| `LIMITS__MAXPDFPAGES` | No | `100` | Páginas máximas de un PDF (1 – 2000). |
| `LIMITS__PDFEXTRACTIONTIMEOUTSECONDS` | No | `15` | Tiempo máximo para extraer el texto de un PDF, en segundos (1 – 120). |
| `LIMITS__MAXCONCURRENTPDFEXTRACTIONS` | No | `2` | PDFs que se leen a la vez como máximo (1 – 64). |
| `LIMITS__MAXWORDSPERSENTENCE` | No | `15` | Palabras por frase a partir de las que se avisa (5 – 50). |
| `LIMITS__MAXOUTPUTTOKENS` | No | `4096` | Tokens máximos por respuesta del modelo (256 – 32 768). |
| `LIMITS__LLMTIMEOUTSECONDS` | No | `120` | Tiempo máximo por llamada al modelo, en segundos (5 – 600). |
| `LIMITS__ADAPTATIONSPERHOUR` | No | `10` | Documentos que puede enviar a adaptar cada usuario por hora (1 – 1000). Al superarlo, 429 con `Retry-After`. |
| `LIMITS__EXPORTSPERMINUTE` | No | `30` | Exportaciones por minuto y usuario (1 – 1000). |
| `EXPORT__FONTSIZEPOINTS` | No | `14` | Letra del texto de los DOCX y PDF exportados, en puntos (14 – 28). |
| `EXPORT__LINESPACING` | No | `1.5` | Interlineado de los DOCX y PDF exportados (1,5 – 3). |
| `EXPORT__DOCXFONTFAMILY` | No | `Arial` | Fuente sin serifa del DOCX. El PDF lleva siempre Atkinson Hyperlegible incrustada. |
| `UI__NOTICE` | No | vacío | Aviso que se muestra como banner en todas las pantallas (máx. 500 caracteres). |
| `TURNSTILE__ENABLED` | No | `false` | Exige el desafío anti-bots de Cloudflare Turnstile al enviar documentos. Si es `false`, nada sale hacia Cloudflare. |
| `TURNSTILE__SITEKEY` | Si está habilitado | vacío | Clave del sitio de Turnstile (pública: la recibe el navegador). |
| `TURNSTILE__SECRETKEY` 🔒 | Si está habilitado | vacío | Clave secreta de Turnstile. Solo en el servidor. |
| `TURNSTILE__TIMEOUTSECONDS` | No | `5` | Tiempo máximo para validar el token, en segundos (1 – 30). Si se agota, se rechaza el envío. |
| `SEED__TESTUSER__EMAIL` | No | vacío | Correo del [usuario de prueba](#usuario-de-prueba). Se crea al arrancar si no existe. |
| `SEED__TESTUSER__PASSWORD` 🔒 | Si hay correo | vacío | Su contraseña: 12 caracteres o más, con mayúscula, minúscula, cifra y símbolo. |
| `AUTH__MAXFAILEDACCESSATTEMPTS` | No | `5` | Intentos fallidos seguidos antes de bloquear la cuenta (3 – 20). |
| `AUTH__LOCKOUTMINUTES` | No | `15` | Minutos que dura el bloqueo (1 – 1440). |
| `AUTH__SESSIONHOURS` | No | `8` | Horas sin actividad tras las que caduca la sesión (1 – 24). |
| `AUTH__LOGINATTEMPTSPERMINUTE` | No | `10` | Intentos de inicio de sesión por minuto y dirección IP (1 – 1000). |
| `SERILOG__MINIMUMLEVEL__DEFAULT` | No | `Information` | Nivel de los logs de la API. |
| `OBSERVABILITY__SENTRYDSN` 🔒 | No | vacío | DSN de Sentry del backend. Vacío: Sentry deshabilitado. |
| `OBSERVABILITY__FRONTENDSENTRYDSN` | No | vacío | DSN de Sentry del frontend (público). Vacío: deshabilitado. |
| `OBSERVABILITY__ENVIRONMENT` | No | `production` | Entorno que se ve en Sentry. |
| `OBSERVABILITY__RELEASE` | No | versión compilada | Versión desplegada (por ejemplo, el `git sha`): en Sentry y en la cabecera `X-App-Version` de `/health`. |
| `OBSERVABILITY__FRONTENDREPORTSPERMINUTE` | No | `10` | Informes de errores del navegador reenviados a Sentry por minuto y por cliente (1 – 1000). |
| `OBSERVABILITY__MAXFRONTENDREPORTBYTES` | No | `200000` | Tamaño máximo de cada informe del navegador, en bytes (1024 – 1 048 576). |

## Observabilidad

Pensada para ver errores, tiempos y qué proveedor procesa cada documento **sin exponer el contenido de nadie**.

- **Logs:** JSON por la consola (Serilog), con el `TraceId` de la petición y, en el proceso de adaptación, el
  `DocumentId`. Cada adaptación deja un único evento (`AdaptationCompleted` o `AdaptationFailed`) con el documento,
  el proveedor (`primary`, `fallback` o `none`), el modelo, los tokens, la duración y el resultado. **Nunca** se registra
  texto de los documentos ni de las respuestas del modelo.
- **Sentry:** deshabilitado sin DSN. El backend envía las excepciones no controladas con su traza; el frontend, los
  errores de JavaScript con el entorno y la versión. Ambos usan un filtro que quita cuerpos, cookies, cabeceras, la
  consulta de las URLs, los datos de la persona y cualquier campo llamado `text`, `content`, `original` o `adapted`.
  El navegador envía sus informes a `/api/monitoring/sentry` y la API los reenvía **solo** al DSN del frontend
  configurado, solo desde el propio origen, solo errores, sesiones e informes del SDK (nada de adjuntos ni
  grabaciones) y con un límite por cliente: la CSP sigue sin permitir conexiones a otros orígenes. Como el DSN del
  frontend es público, **fija también un límite de eventos y de cuota en el proyecto de Sentry del frontend**.
- **Métricas:** formato Prometheus en `/metrics` (adaptaciones por proveedor y resultado, errores por código, duración
  y tokens). Solo responde a peticiones de la red interna (localhost o IPs privadas) que no lleguen por un proxy; el
  nginx público la bloquea y la API no publica ningún puerto. Para consultarla desde la máquina del despliegue:

  ```bash
  docker compose -f deploy/docker-compose.yml exec web wget -qO- http://api:8080/metrics
  ```

  Un Prometheus propio puede leerla uniéndose a la red `frontend` del compose.

## Usuario de prueba

No hay registro de usuarios: las cuentas se crean desde la configuración. Para el usuario de prueba, define en `deploy/.env` `SEED__TESTUSER__EMAIL` y `SEED__TESTUSER__PASSWORD`. Al arrancar, la API lo crea si no existe; si ya existe, no lo toca.

- **Correo del usuario de prueba del TFM:** _pendiente: lo añade la persona responsable al desplegar_. La contraseña se entrega en el formulario del TFM; nunca está en el repositorio.
- Cada persona ve solo sus documentos. Un documento de otra persona responde igual que uno que no existe (404).
- Tras 5 intentos fallidos seguidos la cuenta queda bloqueada 15 minutos. El mensaje de error es siempre el mismo: «Correo o contraseña incorrectos».
- La sesión es una cookie `HttpOnly`, `Secure` y `SameSite=Strict` del propio backend; el navegador no guarda ningún token. Caduca tras 8 horas sin uso y «Salir» la invalida también en el servidor (cierra la sesión en todos los dispositivos de esa persona).
- Las peticiones que cambian algo llevan la cabecera `X-XSRF-TOKEN` con el valor de la cookie `XSRF-TOKEN` (Angular lo hace solo).
- La cookie de sesión es `Secure`: abre la aplicación por HTTPS o en `http://localhost` (los navegadores lo tratan como seguro).

## Proveedores de IA

La aplicación habla con los modelos a través de un único puerto (`ILlmProvider`); cambiar de proveedor es cambiar la configuración, no el código. Hay dos tipos, y cualquiera puede ser principal o respaldo:

- **`OpenAICompatible`**: cualquier servidor con la API de chat de OpenAI. `ENDPOINT` es la URL base de la API (la que acaba en `/v1`) y la clave se envía en la cabecera `Authorization`.
- **`AzureFoundry`**: Azure AI Foundry con su API v1 compatible con OpenAI. `ENDPOINT` es la URL del recurso (la aplicación añade `/openai/v1`) y los "modelos" son los **nombres de los despliegues**.

Cada llamada pide la respuesta en JSON con esquema, sin herramientas, con `LIMITS__MAXOUTPUTTOKENS` y `LIMITS__LLMTIMEOUTSECONDS` como tope y sin redirecciones. La respuesta se valida contra el esquema antes de usarse.

**Ollama** (en otra máquina de tu red; dentro de Docker `localhost` es el contenedor de la API):

```bash
LLM__PRIMARY__TYPE=OpenAICompatible
LLM__PRIMARY__DISPLAYNAME="Servidor propio"
LLM__PRIMARY__ENDPOINT=http://192.168.1.50:11434/v1
LLM__PRIMARY__APIKEY=ollama            # Ollama no comprueba la clave, pero la opción es obligatoria
LLM__PRIMARY__ADAPTERMODEL=nombre-del-modelo-adaptador
LLM__PRIMARY__VERIFIERMODEL=nombre-del-modelo-verificador
```

**llama-swap** (o vLLM, LM Studio…) con clave, detrás de https:

```bash
LLM__PRIMARY__TYPE=OpenAICompatible
LLM__PRIMARY__DISPLAYNAME="Servidor propio"
LLM__PRIMARY__ENDPOINT=https://llm.example.com/v1
LLM__PRIMARY__APIKEY=<tu clave>
LLM__PRIMARY__ADAPTERMODEL=<modelo tal y como aparece en la configuración de llama-swap>
LLM__PRIMARY__VERIFIERMODEL=<otro modelo, de otra familia>
```

**Azure AI Foundry** (aquí como respaldo; crea antes un despliegue por rol):

```bash
LLM__FALLBACK__ENABLED=true
LLM__FALLBACK__TYPE=AzureFoundry
LLM__FALLBACK__DISPLAYNAME=Nube
LLM__FALLBACK__ENDPOINT=https://<tu-recurso>.services.ai.azure.com/   # o https://<tu-recurso>.openai.azure.com/
LLM__FALLBACK__APIKEY=<clave del recurso>
LLM__FALLBACK__ADAPTERMODEL=<nombre del despliegue adaptador>
LLM__FALLBACK__VERIFIERMODEL=<nombre del despliegue verificador>
```

Para comprobar la configuración, arranca la API en `Development` y llama a `GET /dev/llm/ping`: hace una llamada mínima (sin ningún documento) a cada proveedor configurado y a cada rol, y devuelve proveedor, modelo, tokens, duración o el código de error. Esa ruta **no existe** en ningún otro entorno.

```bash
curl -s http://localhost:5125/dev/llm/ping   # dotnet run, perfil http
```

### Principal, respaldo y privacidad

La aplicación funciona siempre igual: **quien la despliega decide** con la configuración qué proveedores se usan, y con ello el equilibrio entre privacidad y disponibilidad. **Por defecto el respaldo está deshabilitado.**

| Configuración | Variables | Qué pasa con los documentos |
|---|---|---|
| **Solo principal local** (por defecto) | `LLM__PRIMARY__*` hacia tu servidor; `LLM__FALLBACK__ENABLED=false` | Ningún documento sale de tu infraestructura. Si el principal cae, **no se llama a ningún otro proveedor**: la llamada falla con "no disponible" y el documento debe esperar a que vuelva (la cola y su reproceso llegan con la adaptación, US-06) |
| **Principal local + respaldo en la nube** | `LLM__PRIMARY__*` hacia tu servidor; `LLM__FALLBACK__ENABLED=true` y `LLM__FALLBACK__*` hacia la nube | Normalmente todo se procesa en tu servidor. Si el principal no responde, los documentos **salen hacia el proveedor de respaldo** mientras dure la caída. Revisa dónde procesa los datos (por ejemplo, Azure con Data Zone EU) |
| **Solo nube** | `LLM__PRIMARY__*` hacia el proveedor en la nube; sin respaldo | Todos los documentos se procesan en ese proveedor |

Cómo se decide en cada llamada:

- El cliente del **principal** tiene un reintento corto ante errores transitorios y un **circuit breaker**: si falla varias veces seguidas, deja de llamarlo durante `LLM__RESILIENCE__BREAKDURATIONSECONDS` y luego deja pasar una petición de prueba. Los umbrales están en `deploy/.env.example` (`LLM__RESILIENCE__*`).
- Cada `LLM__RESILIENCE__HEALTHCHECKINTERVALSECONDS` segundos se comprueba con una petición mínima, **sin ningún documento**, si el principal (y el respaldo, si está habilitado) responde. Si el respaldo está en un proveedor de pago, cada comprobación consume unos pocos tokens; puedes alargar el intervalo o desactivarla con `LLM__RESILIENCE__HEALTHCHECKENABLED=false`.
- Con el respaldo habilitado, si el principal falla o la última comprobación dice que no responde, la llamada va al respaldo; en cuanto el principal vuelve a responder, las llamadas vuelven a él. La respuesta indica qué proveedor la generó con su `DISPLAYNAME`.
- Con el respaldo **deshabilitado**, nunca se le llama, aunque esté configurado (tiene test propio).

Estado visible:

- `GET /health` es la salud de la API y su base de datos (la que usa Docker): no cambia porque el modelo no responda.
- `GET /health/llm` (solo dentro de la red de Docker; nginx no lo publica) devuelve `Degraded` (HTTP 200) si el principal no responde.
- `GET /api/status/llm` devuelve solo si cada proveedor está disponible, sin URLs, IPs ni modelos:

  ```json
  {"primary":{"available":true},"fallback":{"available":true}}
  ```

  `fallback` es `null` si el respaldo no está habilitado.

La decisión está en [ADR 0003](docs/adr/0003-proveedores-configurables.md).

## Documentación

- [Documento de diseño](docs/TFM-Lectura-Facil.md)
- [Backlog y plan del sprint](docs/backlog/README.md)
- [Reglas para agentes de programación](AGENTS.md)
- [Licencias de las dependencias y de la fuente](docs/licencias.md)
- [Ejemplos de exportación en DOCX, PDF, Markdown y texto](docs/ejemplos/US-14/README.md)

## Licencia

[Apache License 2.0](LICENSE).
