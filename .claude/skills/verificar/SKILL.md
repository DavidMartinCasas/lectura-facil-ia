---
name: verificar
description: Ejecuta todos los comandos de verificación del proyecto (build, tests, formato, vulnerabilidades, frontend, secretos, compose) y resume qué pasa y qué falla. Úsala antes de abrir o actualizar una PR, al terminar una historia o cuando pidan "verificar", "comprobar que todo pasa" o "¿está listo?".
---

# Verificar el proyecto

Ejecuta los comandos de la sección 8 de `AGENTS.md` **en este orden** y **sin detenerte en el primer fallo**: el objetivo es un informe completo. Si el proyecto aún no tiene una parte (por ejemplo, no existe `src/LecturaFacil.Web`), márcala como "no aplica", no como fallo.

| # | Comprobación | Comando | Aplica si existe |
|---|---|---|---|
| 1 | Compilación sin avisos | `dotnet build -warnaserror` | `*.sln` / `*.slnx` |
| 2 | Tests y cobertura | `dotnet test --collect:"XPlat Code Coverage"` | proyectos en `tests/` |
| 3 | Formato | `dotnet format --verify-no-changes` | `*.sln` / `*.slnx` |
| 4 | Paquetes vulnerables | `dotnet list package --vulnerable --include-transitive` | `*.sln` / `*.slnx` |
| 5 | Frontend | `cd src/LecturaFacil.Web && npm ci && npm run lint && npm test -- --watch=false && npm run build` | `src/LecturaFacil.Web/package.json` |
| 6 | Auditoría npm | `npm audit --omit=dev --audit-level=high` (en la carpeta del frontend) | idem |
| 7 | Secretos | `gitleaks detect --no-banner` | siempre |
| 8 | Compose | `docker compose -f deploy/docker-compose.yml config --quiet` | `deploy/docker-compose.yml` |
| 9 | Cabeceras SPDX | ficheros `.cs`, `.ts`, `.html` y `.scss` propios sin `SPDX-License-Identifier` (excluye `bin/`, `obj/`, `node_modules/`, `dist/`, código generado y migraciones) | siempre |
| 10 | Librerías prohibidas | ninguna referencia a `MediatR`, `AutoMapper`, `FluentAssertions` ni `QuestPDF` en `*.csproj` ni `Directory.Packages.props` | siempre |

Si una herramienta no está instalada (por ejemplo `gitleaks` o `docker`), **no la instales sin permiso**: indícalo como "no ejecutado" en el informe.

## Cobertura
Si hay informe de cobertura, comprueba que **Domain y Application ≥ 80 %**. No bajes el umbral para que pase.

## Informe
Termina con una tabla:

```markdown
| Comprobación | Resultado | Detalle |
|---|---|---|
| Build | ✅ | |
| Tests | ❌ | 2 fallos: Adapt_WhenProviderReturnsInvalidJson_… |
| … | ⏭️ no aplica | aún no existe el frontend |
```

Después, para cada ❌: la causa probable y la corrección propuesta. **No corrijas nada desde esta skill** salvo que te lo pidan: verificar y corregir son pasos distintos.

## Prohibido para "hacer pasar" la verificación
Saltar o borrar tests, añadir `#pragma warning disable` o `[Fact(Skip=…)]`, bajar umbrales, añadir excepciones a gitleaks o al lint sin explicarlo en la PR.
