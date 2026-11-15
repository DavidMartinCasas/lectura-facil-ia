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
npm start              # http://localhost:4200
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
| `LLM__FALLBACK__ENABLED` | No | `false` | Habilita el proveedor de respaldo. Si es `false`, ningún documento sale del principal. |
| `LLM__FALLBACK__*` | Solo si está habilitado | — | Las mismas opciones que `LLM__PRIMARY__*` (la clave 🔒). |
| `LIMITS__MAXFILESIZEBYTES` | No | `5242880` (5 MB) | Tamaño máximo de fichero (1 KB – 50 MB). |
| `LIMITS__MAXCHARACTERS` | No | `20000` | Caracteres máximos del texto (100 – 200 000). |
| `LIMITS__MAXWORDSPERSENTENCE` | No | `15` | Palabras por frase a partir de las que se avisa (5 – 50). |
| `LIMITS__MAXOUTPUTTOKENS` | No | `4096` | Tokens máximos por respuesta del modelo (256 – 32 768). |
| `LIMITS__LLMTIMEOUTSECONDS` | No | `120` | Tiempo máximo por llamada al modelo, en segundos (5 – 600). |
| `UI__NOTICE` | No | vacío | Aviso que se muestra en la interfaz (máx. 500 caracteres). |

## Documentación

- [Documento de diseño](docs/TFM-Lectura-Facil.md)
- [Backlog y plan del sprint](docs/backlog/README.md)
- [Reglas para agentes de programación](AGENTS.md)

## Licencia

[Apache License 2.0](LICENSE).
