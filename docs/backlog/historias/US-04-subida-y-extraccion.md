# US-04 · Subir un texto o un PDF y extraer su texto

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
**quiero** pegar un texto o subir el PDF de un documento
**para** que la herramienta pueda analizarlo y adaptarlo a lectura fácil.

## Contexto para el agente
- Solo **PDF digital** y texto pegado. El OCR de PDFs escaneados está **fuera del MVP**.
- Modelo de datos: sección 14 de `docs/TFM-Lectura-Facil.md` (entidad `Documento`).
- Estados del documento: sección 13 (`Recibido` → `Procesando` → …).

## Criterios de aceptación
```gherkin
Escenario: Subir texto pegado
  Dado un usuario autenticado
  Cuando envía un texto de 50 a 20.000 caracteres
  Entonces se crea un documento en estado "Recibido"
  Y la respuesta es 202 con el identificador del documento

Escenario: Subir un PDF digital
  Cuando envía un PDF válido de hasta 5 MB
  Entonces se extrae su texto conservando los saltos de párrafo
  Y se crea un documento en estado "Recibido"

Escenario: PDF sin texto (escaneado)
  Cuando envía un PDF cuyas páginas no contienen texto extraíble
  Entonces la respuesta es 422 con el mensaje "Este PDF parece escaneado. De momento solo se admiten PDF con texto."

Escenario: Fichero con extensión falsa
  Cuando envía un fichero llamado "documento.pdf" que no empieza por la firma %PDF-
  Entonces la respuesta es 400
  Y el fichero no se guarda

Escenario: Fichero demasiado grande
  Cuando envía un fichero de más de 5 MB
  Entonces la respuesta es 413 y el fichero no se procesa
```

## Alcance técnico
- **Domain:** entidad `Document` (id, propietario, título, estado, texto original, fechas) con sus transiciones de estado válidas; value objects `DocumentId`, `DocumentTitle`, `OriginalText` (con sus invariantes de longitud).
- **Application:** caso de uso `SubmitDocument` y puertos `ITextExtractor` e `IDocumentRepository`.
- **Infrastructure:** `PdfPigTextExtractor` (con detección de "sin texto"), `EfDocumentRepository`, migración.
- **Api:** `POST /api/documents` con `multipart/form-data` (fichero) o JSON (texto), validado con FluentValidation; límite de tamaño de petición en Kestrel.
- El **fichero original no se guarda en disco**: se procesa en memoria y solo se persiste el texto extraído.

## Seguridad
- **Firma del fichero** (`%PDF-`) comprobada; la extensión y el `Content-Type` no son fiables.
- **Límites** de tamaño de fichero y de caracteres desde `LimitsOptions`, aplicados en Kestrel **y** en la validación.
- PdfPig con **timeout** y limitación de páginas para evitar PDFs que agoten CPU o memoria (DoS).
- **Nombre del fichero del usuario** solo como título saneado (longitud máxima, sin caracteres de control); nunca como ruta.
- Normalización del texto extraído (Unicode NFC, eliminar caracteres de control salvo saltos de línea).
- **Autorización:** el documento queda asociado al usuario autenticado. Hasta que exista la US-15, se usa un propietario fijo de desarrollo detrás de una interfaz `ICurrentUser`.
- El texto **no se registra** en logs; solo id, tamaño y número de páginas.

## Calidad y clean code
- Las reglas de longitud y estado viven en Domain, no en el endpoint.
- El endpoint no conoce PdfPig: solo el puerto `ITextExtractor`.

## Tests requeridos
- [ ] Unitarios de `Document` (transiciones válidas e inválidas) y de los value objects.
- [ ] Unitarios de `SubmitDocument` con dobles de los puertos.
- [ ] Integración de `PdfPigTextExtractor` con PDFs de prueba **generados por los propios tests** o incluidos en `tests/fixtures/` (sin datos personales reales): normal, sin texto, corrupto.
- [ ] Integración del endpoint: 202, 400 (firma falsa), 413, 422.

## Fuera de alcance
- OCR, DOCX, imágenes.
- Lanzar la adaptación (US-06 la engancha a la cola).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Document"
curl -s -X POST http://localhost:8080/api/documents -H "Content-Type: application/json" \
  -d '{"title":"Prueba","text":"Texto de prueba suficientemente largo para pasar la validación mínima de caracteres."}'
```

## Definition of Done específica
- [ ] Ejemplos de PDF de prueba en `tests/fixtures/` sin datos reales.
- [ ] Contrato del endpoint visible en OpenAPI.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-04-subida-y-extraccion.md.
Crea la rama feat/US-04-subida-extraccion, preséntame un plan breve y espera mi confirmación.
Implementa con tests primero en Domain y Application, ejecuta todos los comandos de verificación
y termina con un resumen de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
