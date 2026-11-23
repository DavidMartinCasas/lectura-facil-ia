# US-21 · Registrar la validación humana y publicar

| Campo | Valor |
|---|---|
| **Épica** | EP-05 · Revisión |
| **Prioridad** | Could (adelantada de la versión 2) |
| **Puntos** | 3 |
| **Día previsto** | Lunes 23 |
| **Depende de** | US-13, US-14 |
| **Requiere humano** | No |

## Historia
**Como** técnica de una administración que adapta un documento
**quiero** enviar el borrador revisado a validación, anotar lo que decide el grupo destinatario y publicar la versión validada
**para** completar el ciclo de lectura fácil y dejar constancia de que personas del grupo destinatario lo han entendido.

## Contexto para el agente
- La norma (UNE 153101 EX) pide que validen el texto **personas del grupo destinatario**. Suelen hacerlo en una sesión,
  con el texto impreso, y **no usan la aplicación**. Por eso la validación la anota la misma persona que adaptó el
  documento: no hay cuentas de validador ni roles (eso sigue en la versión 2).
- El dominio ya tenía los estados y las transiciones (sección 13 del diseño): Borrador → Pendiente de validación →
  Validado → Publicado, y Pendiente → Borrador si el grupo pide cambios.
- Solo se envía a validación un borrador con los avisos de datos clave revisados: es lo mismo que pide la exportación,
  y el grupo necesita el documento exportado.
- No se guardan nombres de personas: en «quién lo ha validado» se escribe el grupo.

## Criterios de aceptación
```gherkin
Escenario: Enviar a validación
  Dado un borrador con todos los datos clave revisados
  Cuando pulso "Enviar a validación"
  Entonces el documento pasa a "Pendiente de validación"
  Y ya no se puede editar

Escenario: Avisos pendientes
  Dado un borrador con datos clave sin revisar
  Entonces el botón "Enviar a validación" está desactivado y dice por qué

Escenario: El grupo lo entiende
  Dado un documento pendiente de validación
  Cuando anoto "Sí, lo entiende", quién lo ha validado y, si quiero, observaciones
  Entonces el documento pasa a "Validado" y se ve quién lo validó y cuándo

Escenario: El grupo pide cambios
  Cuando anoto "No, pide cambios"
  Entonces el documento vuelve a "Borrador", se puede editar
  Y se ve lo que pidió el grupo hasta que se vuelva a enviar

Escenario: Retirar de la validación
  Cuando pulso "Volver a borrador" sin anotar nada
  Entonces el documento vuelve a "Borrador" y no se guarda ninguna validación

Escenario: Publicar
  Dado un documento validado
  Cuando pulso "Publicar"
  Entonces pasa a "Publicado" y ya no se puede cambiar

Escenario: Exportar una versión validada
  Dado un documento validado o publicado
  Entonces el fichero exportado lleva el sello "Versión validada" en lugar de "Borrador · pendiente de validación"
```

## Alcance técnico
- **Domain:** `Document.SendToValidation`, `RecordValidation`, `WithdrawFromValidation` y `Publish`; `ValidationRecord`
  (versión, resultado, quién, observaciones y fecha).
- **Infrastructure:** tabla `validations` (migración `AddValidations`), se borra con su documento.
- **Application:** `SendToValidation`, `RecordValidation`, `WithdrawFromValidation`, `PublishDocument` sobre
  `ValidationStep` (buscar del usuario, comprobar, aplicar y guardar con concurrencia optimista).
- **Api:** `POST /api/documents/{id}/validation/send`, `POST /api/documents/{id}/validation`,
  `POST /api/documents/{id}/validation/withdraw` y `POST /api/documents/{id}/publish`. `GET /api/documents/{id}` devuelve
  `canSendToValidation` y la última `validation`.
- **Angular:** `ValidationPanelComponent` en la página del documento: enseña solo el paso que toca según el estado.
  «Mis documentos» filtra por todos los estados.

## Seguridad
- **Autorización por objeto** en los cuatro endpoints (404 si el documento no es del usuario) y **antiforgery**.
- Las observaciones son texto plano, con longitud máxima, y nunca se interpretan como HTML.

## Tests requeridos
- [x] Unitarios de dominio de cada transición y de los límites de los textos.
- [x] Unitarios de los casos de uso (incluidos documento ajeno y conflicto de concurrencia).
- [x] API: ciclo completo, 409 con avisos pendientes, 400 con datos no válidos y matriz BOLA/antiforgery.
- [x] Unitarios del panel y del store en Angular.
- [x] E2E con axe en tema claro y oscuro: enviar → anotar → publicar.

## Fuera de alcance
- Cuentas y roles de validador, grupos y sesiones de validación, comentarios por frase (versión 2).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Validation"
cd src/LecturaFacil.Web && npm test -- --watch=false && npx playwright test e2e/validation-flow.e2e.ts
```
