# US-07 · Revisor de pautas de la adaptación

| Campo | Valor |
|---|---|
| **Épica** | EP-03 · Adaptación |
| **Prioridad** | Must |
| **Puntos** | 2 |
| **Día previsto** | Viernes 20 |
| **Depende de** | US-05, US-06 |
| **Requiere humano** | No |

## Historia
**Como** técnica de una administración
**quiero** saber si la adaptación cumple cada pauta de lectura fácil
**para** confiar en el borrador o saber exactamente qué tengo que corregir.

## Contexto para el agente
- Reutiliza **las mismas reglas** de la US-05 (`IGuidelineChecker`), ahora sobre el texto adaptado.
- Las palabras del glosario cuentan como "explicadas" para la regla de siglas y palabras difíciles.

## Criterios de aceptación
```gherkin
Escenario: Adaptación que cumple todas las pautas
  Dado una versión adaptada sin incumplimientos
  Cuando se revisa
  Entonces cada pauta aparece como cumplida
  Y el resumen indica "12 de 12 pautas cumplidas"

Escenario: Adaptación con un incumplimiento
  Dado una versión adaptada que contiene "40 %"
  Entonces la pauta "SIN_PORCENTAJES" aparece como incumplida con su fragmento y posición

Escenario: Reintento por incumplimientos graves
  Dado una adaptación con incumplimientos de gravedad alta
  Cuando termina la revisión
  Entonces se pide una nueva adaptación una sola vez, indicando al LLM qué pautas incumplió
  Y se guarda la mejor de las dos versiones

Escenario: Sigla explicada en el glosario
  Dado una adaptación con "BOP" que aparece en el glosario
  Entonces "SIN_SIGLAS" se considera cumplida
```

## Alcance técnico
- **Application:** caso de uso `ReviewAdaptation` que ejecuta las reglas sobre la versión y guarda los hallazgos asociados a ella; integración en el pipeline de la US-06 después de adaptar.
- **Domain:** `GuidelineReport` (por pauta: cumplida o no y sus hallazgos) y la política de "mejor versión" (menos incumplimientos graves).
- Reintento único configurable (`LimitsOptions.MaxAdaptationRetries`, por defecto 1).

## Seguridad
- La lista de incumplimientos que se envía al LLM en el reintento se construye con **códigos y descripciones fijas**, no con texto libre del usuario.

## Calidad y clean code
- Sin duplicar reglas: se reutiliza el `GuidelineAnalyzer`.
- La política de selección de versión es una función pura de Domain.

## Tests requeridos
- [ ] Unitarios de `GuidelineReport` y de la política de mejor versión.
- [ ] Unitarios de `ReviewAdaptation` con versiones que cumplen y que no cumplen.
- [ ] Test del reintento con `FakeLlmProvider` (primera respuesta mala, segunda buena).
- [ ] Test de glosario: sigla explicada no cuenta como incumplimiento.

## Fuera de alcance
- Interfaz del informe (US-13).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Review"
```

## Definition of Done específica
- [ ] `GET /api/documents/{id}` incluye el informe de pautas.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-07-revisor-de-pautas.md.
Crea la rama feat/US-07-revisor, preséntame un plan breve y espera mi confirmación.
Reutiliza las reglas de la US-05, sin duplicarlas. Implementa con tests primero, ejecuta todos los
comandos de verificación y termina con un resumen de lo hecho, cómo probarlo, lo pendiente y los riesgos.
No hagas push ni merge.
```
