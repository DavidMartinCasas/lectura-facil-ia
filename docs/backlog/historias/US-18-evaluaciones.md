# US-18 · Evaluaciones de modelos

| Campo | Valor |
|---|---|
| **Épica** | EP-08 · Calidad y operación |
| **Prioridad** | Must |
| **Puntos** | 5 |
| **Día previsto** | Lunes 23 (herramienta) · Martes 24 (ejecución con modelos reales) |
| **Depende de** | US-07, US-08, US-11 |
| **Requiere humano** | Sí: los 10 textos con su adaptación profesional y ejecutar contra los modelos reales |

## Historia
**Como** autor del TFM
**quiero** medir con datos qué modelo adapta mejor y si el verificador detecta los cambios peligrosos
**para** elegir los modelos con criterio y presentar resultados objetivos al tribunal.

## Contexto para el agente
- Plan de evaluación y métricas: sección 18 de `docs/TFM-Lectura-Facil.md`.
- Alcance recortado: **10 textos y 3 modelos** (Gemma-4-26B-A4B, uno de los locales rápidos y `gpt-4.1-mini`).
- **Criterio eliminatorio:** un modelo que cambie o pierda un dato clave sin aviso queda descartado.
- Curso: masterclass "Cómo evaluar LLMs" y módulo de LLMOps.

## Criterios de aceptación
```gherkin
Escenario: Ejecutar la evaluación
  Dado el dataset en evals/datasets/ (original + adaptación profesional por texto)
  Cuando ejecuto "dotnet run --project evals/LecturaFacil.Evals -- --models gemma,qwen,azure-mini"
  Entonces se procesa cada texto con cada modelo
  Y se genera evals/resultados/<fecha>/informe.md y resultados.csv

Escenario: Métricas por modelo
  Entonces el informe muestra por modelo: % de pautas cumplidas, Δ INFLESZ, % de datos clave conservados,
  similitud con la adaptación profesional, tiempo medio y coste estimado

Escenario: Textos trampa
  Dado 5 textos con un dato alterado a propósito en la adaptación
  Cuando se ejecuta el verificador
  Entonces el informe muestra el recall del verificador (avisos detectados / cambios introducidos)

Escenario: Reproducibilidad
  Cuando repito la evaluación con la misma configuración
  Entonces el informe indica versión del prompt, modelo y parámetros usados
```

## Alcance técnico
- Proyecto de consola `evals/LecturaFacil.Evals` que **reutiliza Application e Infrastructure** (los mismos casos de uso que producción, sin duplicar lógica).
- Formato del dataset: un JSON por texto con `id`, `original`, `adaptacion_profesional`, `fuente`, `licencia`.
- Textos trampa generados por el propio proyecto (alterando cifras y plazos de forma controlada).
- Similitud con embeddings (BGE-M3 en local o `text-embedding-3-small` en Azure), detrás de un puerto `IEmbeddingProvider`.
- Informe en Markdown con tablas y CSV con los datos en bruto.
- Opción `--dry-run` con `FakeLlmProvider` para que CI pueda ejecutarlo sin modelos reales.

## Seguridad
- **Solo textos públicos** o sin datos personales; cada texto declara su **fuente y licencia**.
- Las claves de los proveedores salen del entorno, igual que en la API.
- Límite de gasto: la herramienta estima el coste antes de ejecutar contra Azure y pide confirmación (`--yes` para omitirla).

## Calidad y clean code
- Las métricas son funciones puras testeadas.
- Nada de lógica de evaluación dentro de la aplicación web.

## Tests requeridos
- [ ] Unitarios de cada métrica.
- [ ] Unitarios del generador de textos trampa.
- [ ] Ejecución completa en `--dry-run` dentro de CI.

## Fuera de alcance
- Juez LLM con rúbrica (GPT-OSS-20B): **opcional** si sobra tiempo.
- Más de 3 modelos o más de 10 textos.

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Evals"
dotnet run --project evals/LecturaFacil.Evals -- --dry-run
```

## Definition of Done específica
- [ ] Informe real generado por la persona y copiado a `docs/evaluacion/resultados.md`.
- [ ] Tabla de resultados de la sección 18 del documento de diseño rellenada.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-18-evaluaciones.md.
Crea la rama feat/US-18-evaluaciones, preséntame un plan breve y espera mi confirmación.
Reutiliza los casos de uso existentes; no dupliques lógica. Implementa con tests y un modo --dry-run
que funcione en CI sin modelos reales. Ejecuta todos los comandos de verificación y termina con un resumen
de lo hecho, cómo ejecutarlo contra los modelos reales, lo pendiente y los riesgos. No hagas push ni merge.
```
