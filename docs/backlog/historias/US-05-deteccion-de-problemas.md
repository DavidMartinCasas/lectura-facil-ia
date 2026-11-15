# US-05 · Detectar los problemas del texto original

| Campo | Valor |
|---|---|
| **Épica** | EP-02 · Entrada |
| **Prioridad** | Must |
| **Puntos** | 3 |
| **Día previsto** | Miércoles 18 |
| **Depende de** | US-01 |
| **Requiere humano** | No |

## Historia
**Como** técnica de una administración
**quiero** ver qué partes del texto original dificultan la lectura y por qué
**para** entender qué cambia la adaptación y aprender a escribir más claro.

## Contexto para el agente
- Lista de pautas y cómo detectarlas: sección 10 de `docs/TFM-Lectura-Facil.md`.
- **Todo es determinista**: reglas en código, sin LLM. Son las mismas reglas que usará el revisor de pautas (US-07).
- Puede hacerse **en paralelo** con US-04 y US-08.

## Criterios de aceptación
```gherkin
Escenario: Detectar una sigla sin explicar
  Dado el texto "La solicitud se publicará en el BOP."
  Cuando se analizan las pautas
  Entonces hay un hallazgo de la pauta "SIN_SIGLAS" sobre "BOP" con su posición inicial y final

Escenario: Detectar un porcentaje
  Dado el texto "La ayuda será del 40 % de la renta."
  Entonces hay un hallazgo "SIN_PORCENTAJES" sobre "40 %"

Escenario: Detectar una frase larga
  Dado una frase de 32 palabras y un umbral configurado de 15
  Entonces hay un hallazgo "FRASES_CORTAS" con la sugerencia de dividirla

Escenario: Texto sin problemas
  Dado el texto "El Ayuntamiento da ayudas. Puede pedirlas en la web."
  Entonces no hay hallazgos

Escenario: El ejemplo de referencia
  Dado el texto original del ejemplo de la sección 5 del documento de diseño
  Entonces se detectan al menos 9 hallazgos de pautas distintas
```

## Alcance técnico
- **Domain:**
  - Value object `Finding` (código de pauta, fragmento, inicio, fin, gravedad, sugerencia).
  - Interfaz `IGuidelineRule` y **una clase por pauta**: `ShortSentencesRule`, `AcronymsRule`, `PercentagesRule`, `NumbersInDigitsRule`, `PassiveVoiceRule`, `DoubleNegationRule`, `RomanNumeralsRule`, `LegalJargonRule`, `NumericDateRule`.
  - `GuidelineAnalyzer` que aplica todas las reglas registradas.
  - Segmentador de frases para el castellano (abreviaturas comunes como "Sr.", "art.", "núm." no cortan frase).
- **Application:** puerto `IGuidelineChecker` y caso de uso que guarda los hallazgos del original.
- **Infrastructure:** listas configurables (jerga jurídica, siglas permitidas) en ficheros JSON embebidos.
- Catálogo de pautas (`Pauta` del modelo de datos) sembrado en la base de datos.

## Seguridad
- **Expresiones regulares con timeout** (`RegexOptions` + `matchTimeout`) para evitar ReDoS con textos maliciosos.
- Límite de tamaño de entrada ya validado en US-04; las reglas no deben tener coste cuadrático.

## Calidad y clean code
- **Principio abierto/cerrado:** añadir una pauta = añadir una clase que implementa `IGuidelineRule`, sin tocar el analizador.
- Cada regla es pequeña, pura y testeable por separado.
- Posiciones calculadas sobre el texto normalizado, documentado en un comentario de la clase.

## Tests requeridos
- [ ] **TDD:** tests de cada regla con casos positivos, negativos y límite (por ejemplo, "OK" no es una sigla, "40%" y "40 %" y "40 por ciento").
- [ ] Tests del segmentador de frases con abreviaturas.
- [ ] Test con el ejemplo de referencia del documento de diseño.
- [ ] Test de rendimiento básico: 20.000 caracteres en menos de 500 ms.
- [ ] Test de ReDoS: entrada patológica no supera el timeout.

## Fuera de alcance
- Detección de "una idea por frase" con análisis sintáctico avanzado: basta la aproximación por verbos conjugados y conectores de la sección 10.
- Vocabulario frecuente completo (se usa una lista inicial pequeña y ampliable).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Guideline"
```

## Definition of Done específica
- [ ] Tabla de pautas implementadas en `docs/pautas.md` (código, descripción, cómo se detecta).
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-05-deteccion-de-problemas.md.
Crea la rama feat/US-05-deteccion, preséntame un plan breve y espera mi confirmación.
Trabaja en TDD: un test que falla por regla, luego la regla. Ejecuta todos los comandos de verificación
y termina con un resumen de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
