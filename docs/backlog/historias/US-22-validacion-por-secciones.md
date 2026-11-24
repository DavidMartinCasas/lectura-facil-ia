# US-22 · Validación por secciones, rondas y portada validada

| Campo | Valor |
|---|---|
| **Épica** | EP-05 · Revisión |
| **Prioridad** | Could (mejora de US-21) |
| **Puntos** | 3 |
| **Día previsto** | Martes 24 |
| **Depende de** | US-21 |
| **Requiere humano** | No |

## Historia
**Como** técnica que adapta un documento
**quiero** anotar qué partes no entendió el grupo destinatario y cuántas personas lo leyeron, y ver todas las rondas de
validación
**para** saber exactamente dónde cambiar el texto y dejar constancia de cómo se validó.

## Contexto para el agente
- Sigue sin haber cuentas de validador: lo anota la misma persona que adaptó el documento (US-21).
- Las secciones difíciles se envían como posiciones y se guardan como sus preguntas. Así se sigue sabiendo cuáles eran
  aunque después se edite el texto; en el editor se marcan las secciones cuya pregunta coincide.
- No se guardan nombres de personas: solo el grupo y cuántas personas eran.

## Criterios de aceptación
```gherkin
Escenario: Marcar lo que costó entender
  Dado un documento pendiente de validación
  Cuando anoto "No, pide cambios", marco secciones o el glosario, quién lo validó y cuántas personas
  Entonces vuelve a "Borrador" y esas partes se ven marcadas en ámbar en la adaptación con "Costó entender"

Escenario: Solo se marcan partes si pide cambios
  Cuando anoto "Sí, lo entiende"
  Entonces no se ofrece marcar partes y la API rechaza las que lleguen (400)

Escenario: Número de personas
  Entonces es obligatorio, de 1 a 99, y se ve junto a quién lo validó

Escenario: No reenviar sin cambios
  Dado un borrador al que el grupo pidió cambios
  Y que no se ha guardado una versión nueva
  Entonces "Enviar a validación" está desactivado y dice "Cambia el texto antes de volver a enviarlo"
  Y la API responde 409 validation.unchanged

Escenario: Historial de rondas
  Dado un documento con más de una validación
  Entonces se ve cada ronda con su resultado, versión, fecha, grupo, personas y partes difíciles

Escenario: Portada validada
  Dado un documento validado o publicado
  Entonces la portada de DOCX y PDF dice "Validado por <grupo> (<n> personas) el <fecha>"
```

## Alcance técnico
- **Domain:** `GroupDecision`; `ValidationRecord` con `Participants`, `HardSections` y `HardGlossary`;
  `Document.AwaitsChanges` bloquea `SendToValidation`.
- **Infrastructure:** migración `AddValidationDetails` (`participants`, `hard_sections` jsonb, `hard_glossary`).
- **Application:** errores `validation.unchanged` (409) y `validation.unknown_part` (400); `DocumentDetails.Validations`;
  la portada lleva quién validó.
- **Api:** `RecordValidationRequest` con `participants`, `hardSections` y `hardGlossary`; `DocumentResponse.validations`.
- **Angular:** panel de validación (casillas, personas, motivo del bloqueo, rondas) y marcas en el texto adaptado.

## Tests requeridos
- [x] Dominio: partes difíciles permitidas, personas, bloqueo del reenvío y rondas.
- [x] Casos de uso y API: 400 con datos no válidos, 409 al reenviar sin cambios, historial en la respuesta.
- [x] Unitarios del panel y del texto adaptado en Angular.
- [x] E2E con axe en tema claro y oscuro, escritorio y móvil: pedir cambios marca la sección y bloquea el reenvío.
