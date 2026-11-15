# US-13 · Vista lado a lado con hallazgos, avisos y edición

| Campo | Valor |
|---|---|
| **Épica** | EP-05 · Interfaz |
| **Prioridad** | Must |
| **Puntos** | 5 |
| **Día previsto** | Sábado 21 |
| **Depende de** | US-07, US-08, US-11, US-12 |
| **Requiere humano** | No |

## Historia
**Como** técnica de una administración
**quiero** revisar el original y la adaptación lado a lado, con los problemas marcados y los avisos del verificador
**para** corregir lo necesario en pocos minutos y quedarme con un borrador fiable.

## Contexto para el agente
- Referencia visual: la maqueta de la conversación de diseño (texto original con problemas subrayados, adaptación con glosario, paneles de pautas, legibilidad y significado).
- La etiqueta **"Borrador · pendiente de validación"** debe verse siempre.

## Criterios de aceptación
```gherkin
Escenario: Ver la revisión completa
  Dado un documento en estado "Borrador"
  Cuando abro su vista
  Entonces veo el original con los problemas marcados y la adaptación con su glosario
  Y veo los paneles de pautas, legibilidad (antes y después) y datos clave
  Y veo quién lo procesó: "Servidor propio" o "Azure (UE)"

Escenario: Explicación de un problema
  Cuando selecciono un fragmento marcado del original (con ratón o teclado)
  Entonces veo la pauta que incumple y la sugerencia

Escenario: Avisos del verificador
  Dado un dato clave en estado "revisar" u "omitido"
  Entonces aparece destacado con su motivo
  Y no puedo exportar sin marcarlo como revisado

Escenario: Editar la adaptación
  Cuando edito una frase y guardo
  Entonces se crea una nueva versión
  Y se recalculan las pautas y la legibilidad (sin volver a llamar al LLM)

Escenario: Pantalla pequeña
  Dado un móvil
  Entonces original y adaptación se muestran en pestañas en lugar de columnas
```

## Alcance técnico
- **Api:** `PUT /api/documents/{id}/versions/{n}` (edición manual) y `POST /api/documents/{id}/key-facts/{factId}/acknowledge` (marcar dato revisado).
- **Application:** `SaveManualEdit` (nueva versión + revisión de pautas + legibilidad, sin LLM) y `AcknowledgeKeyFact`.
- **Angular:** `DocumentReviewComponent` con `OriginalTextComponent` (fragmentos marcados), `AdaptedTextComponent` (edición por secciones), `GuidelinesPanelComponent`, `ReadabilityPanelComponent`, `KeyFactsPanelComponent`.
- Marcado de fragmentos construyendo **nodos de texto por segmentos** a partir de las posiciones (inicio/fin), no con HTML.

## Seguridad
- **Prohibido** construir el marcado con `innerHTML`: se segmenta el texto en trozos y se pintan con `@for` e interpolación.
- La edición se valida en el servidor (longitudes, estructura de secciones).
- **Autorización por objeto** en la edición y en el acuse de datos clave (404 si el documento no es del usuario).
- **Antiforgery** en las peticiones que modifican estado.

## Calidad y clean code
- Componentes pequeños con una responsabilidad; el contenedor coordina.
- La segmentación del texto es una función pura con sus tests.
- Sin lógica de pautas en el frontend: el recálculo lo hace el backend.

## Tests requeridos
- [ ] Unitarios de la función de segmentación (solapamientos, fragmentos contiguos, texto con emojis y tildes).
- [ ] Unitarios de `SaveManualEdit` y `AcknowledgeKeyFact`.
- [ ] Seguridad: edición de documento ajeno → 404; texto con `<script>` se muestra como texto literal.
- [ ] Accesibilidad (axe) de la vista.
- [ ] E2E: abrir documento → ver aviso → marcar revisado → editar → ver legibilidad recalculada.

## Fuera de alcance
- Exportación (US-14).
- Comentarios por frase de los validadores (versión 2).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~ManualEdit|FullyQualifiedName~Acknowledge"
cd src/LecturaFacil.Web && npm test -- --watch=false && npx playwright test
```

## Definition of Done específica
- [ ] Capturas en la PR (escritorio y móvil).
- [ ] El ejemplo de la sección 5 se ve como en la maqueta de diseño.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-13-vista-lado-a-lado.md.
Crea la rama feat/US-13-vista-revision, preséntame un plan breve (componentes y API) y espera mi confirmación.
No uses innerHTML en ningún caso. Implementa con tests, ejecuta todos los comandos de verificación y termina
con un resumen de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
