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
/// <param name="Cover">Portada de los formatos con páginas (DOCX y PDF). Markdown y texto no la usan.</param>
public sealed record ExportableDocument(
    string Title,
    IReadOnlyList<AdaptedSection> Sections,
    string GlossaryTitle,
    IReadOnlyList<GlossaryTerm> Glossary,
    string Footer,
    string Language,
    ExportCover Cover);

/// <summary>
/// Portada: debajo del título del documento, qué es, el sello de borrador o de versión validada (con quién la validó) y la
/// fecha. No lleva el logo europeo de lectura
/// fácil, que solo pueden usar los textos validados por personas del grupo destinatario, ni metadatos internos.
/// </summary>
/// <param name="Subtitle">Qué es el documento ("Adaptación a lectura fácil").</param>
/// <param name="DraftNotice">
/// Sello del estado: de borrador (el mismo texto que la etiqueta de la aplicación) o de versión validada.
/// </param>
/// <param name="Date">Fecha de la exportación, escrita con letras.</param>
/// <param name="ValidatedBy">
/// En una versión validada, quién la validó, cuántas personas y cuándo; <c>null</c> en un borrador.
/// </param>
public sealed record ExportCover(string Subtitle, string DraftNotice, string Date, string? ValidatedBy = null);
