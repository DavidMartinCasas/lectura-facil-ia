# Licencias de las dependencias

El proyecto se publica con licencia **Apache 2.0** y solo usa dependencias con licencias permisivas (MIT, Apache 2.0,
BSD o equivalentes). Ver las reglas en [`AGENTS.md`](../AGENTS.md#2-stack). Las versiones están fijadas en
[`Directory.Packages.props`](../Directory.Packages.props); las del frontend, en `src/LecturaFacil.Web/package-lock.json`.

Licencias comprobadas en el `.nuspec` de cada paquete publicado en NuGet (noviembre de 2026).

## Backend (producción)

| Paquete | Versión | Licencia | Para qué |
|---|---|---|---|
| DocumentFormat.OpenXml (Open XML SDK) | 3.5.1 | MIT | Exportación a DOCX (US-14) |
| PDFsharp-MigraDoc (y PDFsharp) | 6.2.4 | MIT | Exportación a PDF (US-14) |
| System.Security.Cryptography.Pkcs | 10.0.15 | MIT | Transitiva de PDFsharp, fijada en una versión sin vulnerabilidades conocidas |
| PdfPig | 0.1.16 | Apache 2.0 | Lectura de PDF (US-04) |
| FluentValidation | 12.2.0 | Apache 2.0 | Validación en el borde de la API |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL (permisiva, tipo MIT/BSD) | Base de datos |
| Microsoft.Extensions.AI.OpenAI, Microsoft.Extensions.Http.Resilience | 10.11.0 | MIT | Proveedores de LLM y resiliencia |
| Microsoft.AspNetCore.OpenApi, Microsoft.EntityFrameworkCore.Design, Microsoft.Extensions.* | 10.0.15 | MIT | Plataforma .NET |
| Serilog.AspNetCore (y Serilog, sus sinks y formateadores) | 10.0.0 | Apache 2.0 | Logs en JSON (US-17) |
| Sentry.AspNetCore (y Sentry, Sentry.Extensions.Logging) | 6.12.1 | MIT | Errores del backend en Sentry (US-17) |

> **Atención con PDFsharp-MigraDoc:** desde la versión **6.5.0** el paquete se publica con licencia **AGPL-3.0**, que
> no es compatible con este proyecto. No se debe actualizar por encima de la rama 6.2.x sin revisar antes su licencia.

## Frontend (producción, además de Angular y Angular Material)

| Paquete | Versión | Licencia | Para qué |
|---|---|---|---|
| @sentry/angular (y @sentry/browser, @sentry/core) | 11.10.0 | MIT | Errores de JavaScript en Sentry (US-17) |

## Tests

| Paquete | Versión | Licencia |
|---|---|---|
| xunit.v3 | 4.0.3 | Apache 2.0 |
| Shouldly | 4.3.0 | BSD-3-Clause |
| Testcontainers.PostgreSql | 4.16.0 | MIT |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.15 | MIT |
| coverlet.MTP | 10.1.0 | MIT |
| NetArchTest.Rules | 1.3.2 | El paquete no declara licencia en NuGet; su repositorio la publica como MIT |

## Fuentes

| Fuente | Licencia | Dónde |
|---|---|---|
| [Atkinson Hyperlegible](https://github.com/googlefonts/atkinson-hyperlegible) (Braille Institute), regular y negrita | SIL Open Font License 1.1 | `src/LecturaFacil.Infrastructure/Export/Fonts/`, con su `OFL.txt`. Se incrusta en los PDF exportados |

La OFL permite usar, incrustar y redistribuir la fuente, también dentro de software con otra licencia, siempre que se
incluya su licencia y no se venda la fuente sola. El DOCX no lleva fuentes dentro: usa una fuente sin serifa del sistema
(`Export:DocxFontFamily`, por defecto Arial).
