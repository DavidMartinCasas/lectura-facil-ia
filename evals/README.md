# Evaluación de modelos (US-18)

Herramienta de consola que mide qué modelo adapta mejor y si el verificador detecta los cambios peligrosos. Usa **los
mismos casos de uso que la aplicación** (`SubmitDocument`, `AdaptDocument` con el revisor de pautas, el reintento y la
verificación, `CompareReadability` y `VerifyMeaning`), con un repositorio en memoria: no necesita base de datos.

## Ejecutar

Desde la raíz del repositorio:

```bash
# Simulación: sin modelos ni claves (es lo que ejecuta la CI con los textos de ejemplo).
dotnet run --project evals/LecturaFacil.Evals -- --dry-run

# Modelos reales: los perfiles de evals/modelos.json.
export EVALS_LOCAL_ENDPOINT=http://localhost:8080/v1   # su servidor compatible con OpenAI
export EVALS_LOCAL_API_KEY=...
export EVALS_AZURE_ENDPOINT=https://<su-recurso>.openai.azure.com/
export EVALS_AZURE_API_KEY=...
export EVALS_EMBEDDINGS_ENDPOINT=http://localhost:8080/v1
export EVALS_EMBEDDINGS_API_KEY=...
dotnet run --project evals/LecturaFacil.Evals -- --models gemma,qwen,azure-mini
```

Opciones: `--models`, `--dry-run`, `--yes`, `--dataset`, `--output`, `--config` y `--help`.

El resultado queda en `evals/resultados/<fecha>/informe.md` (tablas) y `resultados.csv` (datos en bruto). Las
simulaciones van a `evals/resultados/<fecha>-dry-run/`, que no se versiona.

## Perfiles de modelos (`evals/modelos.json`)

Cada perfil es un proveedor (`OpenAICompatible` o `AzureFoundry`) con su modelo adaptador y su verificador, como en la
configuración de la API, y su precio por millón de tokens (0 en local). **El fichero no lleva direcciones ni claves:**
nombra las variables de entorno de las que se leen. Se valida con las mismas reglas que la API (https, o http solo
hacia localhost o una red privada).

- El verificador debe ser de **otra familia** que el adaptador (ADR 0002): por eso el perfil `qwen` verifica con Gemma.
- `embeddings` es opcional. Sin él no se mide la similitud. Es el mismo modelo para todos los perfiles.
- **Límite de gasto:** si algún perfil tiene precio, la herramienta muestra una cota superior del coste y pide
  confirmación antes de llamar a nada. `--yes` la omite.

## Dataset (`evals/datasets/`)

Un JSON por texto, en la carpeta `evals/datasets/` (las subcarpetas no se leen):

```json
{
  "id": "ayuda-alquiler",
  "original": "Texto administrativo original…",
  "adaptacion_profesional": "Título\nUna frase por línea…",
  "fuente": "Dónde se ha publicado (URL o referencia)",
  "licencia": "Licencia o permiso de uso"
}
```

- **Solo textos públicos** o sin datos personales, con su **fuente y licencia**.
- Como mucho 20 textos (el alcance son 10). Si un fichero no es válido no se evalúa nada.
- `evals/datasets/ejemplos/` tiene dos textos **inventados** para la simulación y los tests. No son adaptaciones
  profesionales y no deben usarse para sacar conclusiones.

## Métricas

| Métrica | Cómo se mide |
|---|---|
| Pautas cumplidas | Revisor determinista sobre la adaptación guardada |
| Δ INFLESZ | INFLESZ de la adaptación − INFLESZ del original |
| Datos clave conservados | Conservados / datos listados por la verificación (verificador + cifras por código) |
| Similitud | Coseno de los embeddings de la adaptación y la adaptación profesional |
| Tiempo y coste | Tiempo de reloj por texto; tokens de todas las llamadas × precio del perfil |
| Recall del verificador | Textos trampa con aviso / textos trampa |

**Textos trampa:** en la adaptación profesional de los 5 primeros textos que tengan una cifra entera escrita con
dígitos, esa cifra se cambia por la siguiente (`15 días` → `16 días`). Se pasa la adaptación alterada por la
verificación y se comprueba si algún aviso cita la cifra original o la cambiada.

**Criterio eliminatorio:** un modelo que cambie o pierda un dato clave sin aviso queda descartado. La herramienta no
puede detectar lo que nadie ha avisado: hay que revisar a mano una muestra de los datos marcados como conservados.

El juez LLM con rúbrica queda fuera de esta versión (opcional en la historia).
