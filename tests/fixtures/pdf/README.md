# PDFs de prueba

Generados con el `PdfDocumentBuilder` de PdfPig. Contenido **ficticio**, sin datos personales.

| Fichero | Qué es | Resultado esperado |
|---|---|---|
| `normal.pdf` | PDF digital de 2 páginas y 3 párrafos | Se extrae el texto con los párrafos separados por una línea en blanco |
| `sin-texto.pdf` | Una página con solo un rectángulo dibujado, sin capa de texto (como un escaneado) | 422 · "Este PDF parece escaneado…" |
| `corrupto.pdf` | Empieza por la firma `%PDF-` pero el resto no es un PDF válido | 422 · no se puede leer |

Las fuentes estándar del generador no admiten tildes: por eso el texto va sin ellas.
