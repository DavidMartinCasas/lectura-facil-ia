// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Application.Export;

/// <summary>
/// Modelo intermedio común a todos los formatos de exportación. Lo construye <see cref="ExportDocument"/>; cada
/// <see cref="IDocumentExporter"/> solo lo traduce a su formato. Todo es texto plano: ningún campo es HTML ni Markdown y
/// cada exportador lo escapa según su formato. No lleva metadatos internos (proveedor, modelo, servidor ni usuario).
/// </summary>
/// <param name="Title">Título de la adaptación.</param>
/// <param name="Sections">Secciones: una pregunta que hace de encabezado y una frase por línea.</param>
/// <param name="GlossaryTitle">Encabezado del glosario, que va al final.</param>
/// <param name="Glossary">Palabras difíciles y su explicación. Puede estar vacío.</param>
/// <param name="Footer">Aviso de borrador con la fecha. En DOCX y PDF va en el pie de cada página; en Markdown y texto, al final.</param>
/// <param name="Language">Idioma del documento (BCP 47), que declaran los formatos que lo admiten.</param>
public sealed record ExportableDocument(
    string Title,
    IReadOnlyList<AdaptedSection> Sections,
    string GlossaryTitle,
    IReadOnlyList<GlossaryTerm> Glossary,
    string Footer,
    string Language);
