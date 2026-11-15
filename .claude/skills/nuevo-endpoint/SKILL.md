---
name: nuevo-endpoint
description: Receta para añadir o modificar un endpoint de la API (Minimal APIs de ASP.NET Core) con caso de uso, validación, autorización por documento (BOLA → 404), límites de peticiones, ProblemDetails y tests. Úsala cuando una historia pida un endpoint nuevo en src/LecturaFacil.Api o cambie uno existente.
---

# Añadir un endpoint

Objetivo: endpoints **delgados** y **seguros por defecto**. La lógica va en un caso de uso de `Application`; el endpoint solo valida, llama y traduce el resultado.

## Lista de pasos

1. **Caso de uso (Application)** — test primero.
   - Clase simple inyectada (`AdaptDocument`, `GetDocument`…), **sin MediatR**.
   - Recibe un `record` de entrada y el **id del usuario actual**; devuelve `Result<T>` con errores tipados (`NotFound`, `Validation`, `ProviderUnavailable`…).
   - **La comprobación de propiedad va aquí o en el repositorio**, no en el endpoint: `repository.GetForOwnerAsync(documentId, userId, ct)`. Un documento de otro usuario devuelve **`NotFound`**, igual que uno inexistente (no reveles que existe).

2. **DTOs** — `record` de petición y respuesta en `Application` (o `Api/Contracts` si son solo de transporte). Mapeo **a mano**, sin AutoMapper. Nunca expongas entidades de EF.

3. **Validación** — un `AbstractValidator<TRequest>` de FluentValidation con longitudes, formatos y rangos. Los límites (tamaño máximo, nº de caracteres) salen de `Options`, no son literales.

4. **Endpoint (Api)** — en un fichero por grupo de rutas (`DocumentEndpoints.cs`) con un método de extensión `MapDocumentEndpoints`.
   - Agrupa con `MapGroup("/api/documents")` y aplica al grupo `RequireAuthorization()`.
   - Si llama a un LLM o es costoso: `RequireRateLimiting("<política>")` con una política definida en la configuración del `RateLimiter`.
   - Si cambia estado y se usa con cookies: protección **antiforgery**.
   - Devuelve `TypedResults` y traduce `Result` → `200/201`, `400` (ValidationProblem), `404`, `503`… con **ProblemDetails genéricos**: sin trazas, rutas, nombres de modelo ni de servidor.
   - Comprueba en la documentación de .NET 10 la firma exacta de cualquier API de la que no estés seguro; **no la inventes**.

5. **Registro en DI** — el caso de uso y el validador en el método de composición correspondiente (`AddApplication`, `AddInfrastructure`…), no sueltos en `Program.cs`.

6. **OpenAPI** — nombre, resumen y respuestas posibles (incluidas `400`, `401`, `404`, `429`).

7. **Logs** — solo IDs, tamaños, tiempos y resultado. **Nunca** el texto del documento ni el del LLM.

## Tests obligatorios
| Test | Tipo |
|---|---|
| Caso de uso: camino feliz y cada error del `Result` | Unitario (Application) |
| Validador: casos válidos, inválidos y límite | Unitario |
| Endpoint sin autenticar → `401` | Integración (`WebApplicationFactory`) |
| Documento de **otro usuario** → `404` (BOLA) | Integración, con dos usuarios sembrados |
| Entrada inválida → `400` con ProblemDetails sin detalles internos | Integración |
| Si tiene límite: superar el límite → `429` | Integración |
| Si llama al LLM: usa `FakeLlmProvider`, nunca un modelo real | Integración |

Base de datos real con **Testcontainers** (PostgreSQL), no el proveedor en memoria de EF.

## Checklist rápida antes del commit
- [ ] `// SPDX-License-Identifier: Apache-2.0` en los ficheros nuevos.
- [ ] Ninguna lógica de negocio en `Api`.
- [ ] `CancellationToken` desde el endpoint hasta el repositorio.
- [ ] El test de arquitectura (NetArchTest) sigue en verde.
