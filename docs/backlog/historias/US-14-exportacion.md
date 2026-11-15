# US-14 · Exportar en DOCX, PDF, Markdown o texto

| Campo | Valor |
|---|---|
| **Épica** | EP-06 · Exportación |
| **Prioridad** | Must |
| **Puntos** | 5 |
| **Día previsto** | Domingo 22 |
| **Depende de** | US-13 |
| **Requiere humano** | No |

## Historia
**Como** técnica de una administración
**quiero** descargar la adaptación en el formato que necesite: DOCX, PDF, Markdown o texto
**para** seguir editándola en mi procesador de textos, enviarla a validación, publicarla en la web o integrarla en otros sistemas.

## Contexto para el agente
- Cada formato tiene su uso:

| Formato | Para qué | Librería |
|---|---|---|
| **DOCX** | Seguir editando en Word o LibreOffice. **Es el formato principal** | Open XML SDK (`DocumentFormat.OpenXml`, MIT) |
| **PDF** | Enviar a validación o publicar tal cual | PDFsharp + MigraDoc (MIT) |
| **Markdown** | Publicar en webs, wikis o repositorios | Sin librerías |
| **Texto** | Copiar en cualquier sitio o integrar con otros sistemas | Sin librerías |

- **Solo librerías con licencia permisiva** (ver `AGENTS.md`). **No usar QuestPDF.**
- Maquetación de lectura fácil para DOCX y PDF: letra sin serifa y grande, interlineado amplio, texto alineado a la izquierda (sin justificar), una frase por línea, títulos destacados y glosario al final.
- **Prioridad dentro de la historia:** DOCX, Markdown y texto primero; PDF el último. Si falta tiempo, el PDF pasa a la tabla de recortes (el usuario puede generar el PDF desde el DOCX).

## Criterios de aceptación
```gherkin
Escenario: Elegir formato
  Dado un documento en "Borrador" con todos los datos clave revisados
  Cuando elijo un formato (DOCX, PDF, Markdown o texto) y pulso "Exportar"
  Entonces descargo el fichero con la extensión y el tipo MIME correctos
  Y contiene el título, las secciones con una frase por línea y el glosario

Escenario: Aviso de borrador
  Entonces todos los formatos incluyen "Borrador · pendiente de validación" y la fecha
  (en DOCX y PDF, en el pie de cada página; en Markdown y texto, al final)

Escenario: Bloqueo por avisos pendientes
  Dado un documento con datos clave en "revisar" sin marcar
  Cuando intento exportar en cualquier formato
  Entonces la respuesta es 409 con el mensaje "Revisa los avisos antes de exportar"

Escenario: Formato no soportado
  Cuando pido el formato "exe"
  Entonces la respuesta es 400

Escenario: Maquetación accesible (DOCX y PDF)
  Entonces se usa una fuente sin serifa de al menos 14 pt, interlineado ≥ 1,5 y alineación a la izquierda
  Y los títulos usan estilos de encabezado reales (navegables con lector de pantalla)
  Y el documento declara título e idioma "es"

Escenario: Markdown seguro
  Dado una adaptación cuyo texto contiene "![x](https://ejemplo.com/a.png)" o "<script>"
  Cuando exporto a Markdown
  Entonces esos fragmentos aparecen escapados como texto literal, sin enlaces ni imágenes activos
```

## Alcance técnico
- **Application:** caso de uso `ExportDocument(documentId, format)` y puerto `IDocumentExporter` con `Format`, `ContentType` y `FileExtension`.
- **Infrastructure/Export:** una clase por formato (**patrón estrategia**): `DocxExporter`, `PdfExporter`, `MarkdownExporter`, `PlainTextExporter`, y un registro que elige la estrategia por formato.
- Un **modelo intermedio** común (`ExportableDocument`: título, secciones, glosario, pie) que construye Application; los exportadores solo lo traducen a su formato.
- Fuente con licencia libre incluida en el proyecto (por ejemplo, **Atkinson Hyperlegible**, OFL) y registrada para el PDF.
- **Api:** `POST /api/documents/{id}/export?format=docx|pdf|md|txt`.
- **Angular:** selector de formato con DOCX por defecto.
- Parámetros de maquetación (tamaño de letra, interlineado, márgenes) en `Options`.

## Seguridad
- **Autorización por objeto** (404 si el documento no es del usuario).
- **Lista blanca de formatos**; cualquier otro valor, 400.
- **Escapado por formato:**
  - Markdown: escapar caracteres especiales y neutralizar enlaces, imágenes y HTML (**OWASP LLM05**: un enlace o imagen generado por el LLM podría filtrar datos al abrirse en otro visor).
  - DOCX y PDF: el texto se inserta como texto, nunca como campos, hipervínculos ni macros.
- Nombre del fichero de descarga **saneado** y cabecera `Content-Disposition` correcta.
- Los ficheros solo contienen la adaptación: sin metadatos internos (proveedor, servidor, rutas, usuario).
- Límite de peticiones en el endpoint.

## Calidad y clean code
- **Abierto/cerrado:** añadir un formato = añadir una estrategia, sin tocar el caso de uso.
- Sin lógica de negocio en los exportadores: solo traducen el modelo intermedio.
- Sin generar ficheros en el controlador ni en el frontend.

## Tests requeridos
- [ ] Unitarios de `ExportDocument` (bloqueo por avisos, formato no soportado, éxito).
- [ ] Unitarios de `MarkdownExporter` y `PlainTextExporter`, incluido el escapado de enlaces, imágenes y HTML.
- [ ] Integración de `DocxExporter`: el fichero se abre con Open XML SDK, tiene estilos de encabezado, idioma "es" y el pie.
- [ ] Integración de `PdfExporter`: el PDF contiene el título y el pie (comprobado extrayendo el texto con PdfPig).
- [ ] Seguridad: exportar documento ajeno → 404.

## Fuera de alcance
- Plantillas por organización (logos, colores) y ODT: versión 2.
- Pictogramas: versión 2.

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Export"
```

## Definition of Done específica
- [ ] Un ejemplo de cada formato adjunto a la PR.
- [ ] Licencias de las librerías y de la fuente en `docs/licencias.md`.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-14-exportacion.md.
Crea la rama feat/US-14-exportacion, preséntame un plan breve y espera mi confirmación.
Comprueba en la documentación oficial las APIs actuales de Open XML SDK y de PDFsharp/MigraDoc antes de usarlas.
Implementa en este orden: modelo intermedio, texto, Markdown, DOCX y por último PDF, con tests en cada paso.
Ejecuta todos los comandos de verificación y termina con un resumen de lo hecho, cómo probarlo,
lo pendiente y los riesgos. No hagas push ni merge.
```
