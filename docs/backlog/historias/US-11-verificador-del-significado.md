# US-11 · Verificador del significado

| Campo | Valor |
|---|---|
| **Épica** | EP-04 · Verificación |
| **Prioridad** | Must |
| **Puntos** | 5 |
| **Día previsto** | Viernes 20 |
| **Depende de** | US-06 |
| **Requiere humano** | No |

## Historia
**Como** técnica de una administración
**quiero** que la herramienta compruebe que la adaptación conserva todos los datos importantes del original
**para** no publicar nunca un documento con un plazo, un importe o un requisito cambiado o perdido.

## Contexto para el agente
- Diseño completo: sección 11 de `docs/TFM-Lectura-Facil.md` (diagrama, JSON de ejemplo y reglas de diseño).
- El verificador usa el rol `Verifier` de `ILlmProvider`, que se configura con **un modelo de otra familia** que el adaptador.
- Es la red de seguridad del sistema: **ningún dato clave puede cambiar sin generar un aviso**.

## Criterios de aceptación
```gherkin
Escenario: Dato conservado
  Dado un original con "máximo de 250 €" y una adaptación con "Como mucho, 250 euros al mes"
  Cuando se verifica
  Entonces el dato "importe" aparece como "conservado"

Escenario: Dato cambiado
  Dado un original con "quince días hábiles" y una adaptación con "10 días"
  Entonces el dato "plazo" aparece como "revisar" con el motivo "El valor ha cambiado"

Escenario: Dato omitido
  Dado un original con un requisito que no aparece en la adaptación
  Entonces el dato aparece como "omitido"

Escenario: Conversión calculada
  Dado un original con "2,5 veces el IPREM" y una adaptación con "1.500 euros"
  Entonces el dato aparece como "revisar" con el motivo "Conversión calculada; debe confirmarla una persona"

Escenario: El código manda sobre el LLM
  Dado que la comprobación numérica por código detecta que 250 se ha convertido en 300
  Y el LLM verificador dice "conservado"
  Entonces el dato queda como "revisar"
```

## Alcance técnico
- **Domain:**
  - Entidad `KeyFact` (tipo, valor original, valor adaptado, estado, motivo) con estados `Conserved`, `Review`, `Omitted`.
  - `NumberNormalizer` para el castellano: "quince" → 15, "250 €" → 250, "1.500" → 1500, "2,5" → 2.5, fechas "15 de diciembre de 2026" ↔ 15/12/2026.
  - `NumericConsistencyCheck`: compara las cifras de cada dato clave en original y adaptación.
  - **Regla de combinación:** el resultado final es el **más grave** entre el código y el LLM. El LLM **solo puede añadir** avisos.
- **Application:** caso de uso `VerifyMeaning` en el pipeline (en paralelo con la US-07); puerto `IMeaningVerifier`.
- **Infrastructure:** prompt `Prompts/verify.system.md` y esquema `Prompts/verify.schema.json` (dos pasos: extraer datos clave del original y localizarlos en la adaptación).

## Seguridad
- **OWASP LLM01:** original y adaptación entre delimitadores distintos (`<original>`, `<adaptacion>`) y marcados como datos.
- **OWASP LLM09 (desinformación):** el diseño asume que el LLM puede equivocarse; por eso la comprobación numérica por código y la regla de "solo añade avisos".
- Validación de la salida contra el esquema; si no valida, todos los datos quedan como "revisar" (fallo seguro).

## Calidad y clean code
- `NumberNormalizer` y `NumericConsistencyCheck` son funciones puras con TDD exhaustivo.
- El verificador no modifica la adaptación: solo informa.

## Tests requeridos
- [ ] **TDD** de `NumberNormalizer`: números en letra hasta mil, decimales con coma, miles con punto, euros, porcentajes, fechas.
- [ ] Unitarios de la regla de combinación (código vs. LLM).
- [ ] Unitarios de `VerifyMeaning` con `FakeLlmProvider`: conservado, cambiado, omitido, JSON inválido (todo a "revisar").
- [ ] Test con el ejemplo completo de la sección 5 del documento de diseño: el IPREM queda en "revisar" y la ordenanza en "omitido".

## Fuera de alcance
- Corrección automática de la adaptación.
- Interfaz de los avisos (US-13).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Verify|FullyQualifiedName~Number"
```

## Definition of Done específica
- [ ] ADR `docs/adr/0002-verificador-de-otra-familia.md`.
- [ ] `GET /api/documents/{id}` incluye la lista de datos clave.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-11-verificador-del-significado.md.
Crea la rama feat/US-11-verificador, preséntame un plan breve (incluye el borrador del prompt y del esquema)
y espera mi confirmación. Empieza en TDD por NumberNormalizer y la regla de combinación.
Ejecuta todos los comandos de verificación y termina con un resumen de lo hecho, cómo probarlo,
lo pendiente y los riesgos. No hagas push ni merge.
```
