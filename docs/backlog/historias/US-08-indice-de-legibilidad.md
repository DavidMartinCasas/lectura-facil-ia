# US-08 · Índice de legibilidad INFLESZ

| Campo | Valor |
|---|---|
| **Épica** | EP-03 · Adaptación |
| **Prioridad** | Must |
| **Puntos** | 3 |
| **Día previsto** | Jueves 19 |
| **Depende de** | US-01 |
| **Requiere humano** | No |

## Historia
**Como** técnica de una administración
**quiero** ver cuánto más fácil de leer es la adaptación que el original
**para** justificar el cambio con un dato objetivo.

## Contexto para el agente
- Fórmulas y escala: sección 12 de `docs/TFM-Lectura-Facil.md`.
  - Szigriszt-Pazos: `IFSZ = 206,835 − 62,3·(sílabas/palabras) − (palabras/frases)`.
  - Escala INFLESZ: < 40 muy difícil; 40–55 algo difícil; 55–65 normal; 65–80 bastante fácil; > 80 muy fácil.
- Necesita un **contador de sílabas para el castellano** basado en reglas (diptongos, hiatos con tilde, triptongos, grupos consonánticos).
- No depende de otras historias de negocio: puede hacerse **en paralelo**.

## Criterios de aceptación
```gherkin
Escenario: Contar sílabas
  Dado las palabras "ayuntamiento", "día", "construcción", "guion", "país"
  Cuando cuento sus sílabas
  Entonces obtengo 5, 2, 3, 1 y 2 sílabas respectivamente
  # "guion" cuenta como 1 sílaba según el criterio ortográfico de la RAE (2010)

Escenario: Calcular el índice
  Dado un texto de referencia con sílabas, palabras y frases conocidas
  Cuando calculo el índice
  Entonces el resultado coincide con el valor esperado con una tolerancia de ±1

Escenario: Clasificar
  Dado un índice de 72
  Entonces el grado es "Bastante fácil"

Escenario: Antes y después
  Dado un documento con su versión adaptada
  Entonces se guardan el índice del original y el de la adaptación
```

## Alcance técnico
- **Domain:** `SpanishSyllableCounter`, `ReadabilityScore` (valor + grado), `SzigrisztPazosCalculator`.
- **Application:** puerto `IReadabilityCalculator` y su uso en el pipeline tras adaptar.
- **Infrastructure:** nada específico (es lógica pura), salvo el registro en DI.
- Fernández Huerta: **opcional**, solo si sobra tiempo (está en la tabla de recortes).

## Seguridad
- Lógica pura sin entrada externa nueva. Mantener el coste lineal con el tamaño del texto.

## Calidad y clean code
- La palabra "guion" (monosílaba según la RAE desde 2010) y casos dudosos se documentan en un comentario y en los tests.
- El índice se marca en la interfaz como **aproximado**.

## Tests requeridos
- [ ] **TDD** del contador de sílabas con al menos 40 palabras: diptongos, hiatos, triptongos, `qu`/`gu`, `ll`, `rr`, `ch`, grupos `pr`/`bl`/`tr`, palabras con tilde.
- [ ] Tests de la fórmula con textos de referencia calculados a mano.
- [ ] Tests de la clasificación en los límites (40, 55, 65, 80).

## Fuera de alcance
- Otros índices (Flesch adaptado, μ legibilidad).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Readability|FullyQualifiedName~Syllable"
```

## Definition of Done específica
- [ ] Documentadas en el código las reglas de silabeo implementadas y sus limitaciones.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-08-indice-de-legibilidad.md.
Crea la rama feat/US-08-inflesz, preséntame un plan breve y espera mi confirmación.
Trabaja en TDD estricto: lista de palabras con su número de sílabas primero, luego el contador.
Ejecuta todos los comandos de verificación y termina con un resumen de lo hecho, cómo probarlo,
lo pendiente y los riesgos. No hagas push ni merge.
```
