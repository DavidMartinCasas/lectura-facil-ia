# US-01 · Esqueleto de la solución

| Campo | Valor |
|---|---|
| **Épica** | EP-01 · Base |
| **Prioridad** | Must |
| **Puntos** | 5 |
| **Día previsto** | Lunes 16 |
| **Depende de** | — |
| **Requiere humano** | No (el repositorio vacío en GitHub ya existe) |

## Historia
**Como** desarrollador del proyecto
**quiero** una solución con Clean Architecture, frontend, base de datos y CI funcionando desde el primer día
**para** que el resto de historias se construyan sobre una base ordenada, verificable y segura.

## Contexto para el agente
- Estructura y reglas en `AGENTS.md` (secciones 3 y 4).
- Diseño: `docs/TFM-Lectura-Facil.md`, secciones 7 (Arquitectura) y 20 (Estructura del repositorio).

## Criterios de aceptación
```gherkin
Escenario: La solución compila y pasa los tests
  Dado el repositorio recién clonado
  Cuando ejecuto "dotnet build -warnaserror" y "dotnet test"
  Entonces ambos terminan sin errores
  Y existe al menos un test por capa

Escenario: La regla de dependencias está protegida
  Dado el proyecto de tests de arquitectura
  Cuando un proyecto de Domain referencia a Infrastructure
  Entonces el test de arquitectura falla

Escenario: Todo arranca con Docker Compose
  Dado Docker instalado y un fichero .env creado a partir de .env.example
  Cuando ejecuto "docker compose -f deploy/docker-compose.yml up --build"
  Entonces arrancan PostgreSQL, la API y el frontend
  Y GET /health responde 200 con {"status":"Healthy"}
  Y el frontend muestra una página de inicio con el nombre del proyecto

Escenario: CI básico
  Dado un push a una rama o una PR
  Cuando se ejecuta el workflow de GitHub Actions
  Entonces compila backend y frontend, ejecuta los tests y gitleaks
```

## Alcance técnico
- `LecturaFacil.sln` con los proyectos de `src/` y `tests/` (ver `AGENTS.md`).
- `Directory.Build.props`: `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest`.
- `Directory.Packages.props` con **gestión central de versiones** de NuGet.
- `.editorconfig` con estilo de C# (namespaces de archivo, `var` donde sea evidente, etc.).
- `LecturaFacil.Api`: Minimal API con `/health` (health checks, incluida la base de datos), OpenAPI integrado, ProblemDetails, cabeceras de seguridad.
- `LecturaFacil.Infrastructure`: `AppDbContext` vacío con Npgsql y primera migración.
- `src/LecturaFacil.Web`: Angular *standalone* + Angular Material, ESLint, página de inicio. (PrimeNG v22+ dejó de ser MIT; ver PR de la US-01.)
- `tests/LecturaFacil.Architecture.Tests` con NetArchTest para la regla de dependencias.
- `deploy/docker-compose.yml`, `deploy/.env.example`, `Dockerfile` de API (multi-stage, usuario no root) y de Web (nginx sin root, sirve Angular y hace de proxy a `/api`).
- `.github/workflows/ci.yml`: build, tests, lint, gitleaks.
- `.gitignore` que excluya `.env`, `bin/`, `obj/`, `node_modules/`.

## Seguridad
- **Ningún secreto** en el repositorio: la contraseña de PostgreSQL viene de `.env`; `.env.example` lleva valores de ejemplo evidentes (`cambia-esto`).
- Contenedores con **usuario no root** e imágenes base mínimas.
- PostgreSQL **no expone su puerto** fuera de la red de Docker en el compose de producción.
- Cabeceras de seguridad desde el principio (CSP restrictiva, `nosniff`, `Referrer-Policy`).
- ProblemDetails sin detalles internos fuera de `Development`.

## Calidad y clean code
- Proyectos vacíos pero con la **composición de dependencias** por capa (`AddApplication()`, `AddInfrastructure()`), no todo en `Program.cs`.
- Nada de código de ejemplo de plantillas (WeatherForecast, etc.).

## Tests requeridos
- [ ] Test de arquitectura: Domain no depende de nada; Application no depende de Infrastructure ni de ASP.NET Core.
- [ ] Test de integración de `/health` con `WebApplicationFactory` y Testcontainers.
- [ ] Un test unitario mínimo en Domain y otro en Application (pueden ser de un value object `DocumentId`).
- [ ] Test del frontend: la página de inicio se renderiza.

## Fuera de alcance
- Autenticación, LLM, PDFs, entidades de negocio completas.
- Despliegue en el VPS (es la US-10).

## Verificación
```bash
dotnet build -warnaserror && dotnet test
docker compose -f deploy/docker-compose.yml up --build -d
curl -s http://localhost:8080/health
docker compose -f deploy/docker-compose.yml down
```

## Definition of Done específica
- [ ] El README tiene una sección mínima "Cómo arrancar en local".
- [ ] El workflow de CI está en verde en la PR.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-01-esqueleto-solucion.md.
Crea la rama feat/US-01-esqueleto, preséntame un plan breve y espera mi confirmación.
Implementa con tests, ejecuta todos los comandos de verificación y termina con un resumen
de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
