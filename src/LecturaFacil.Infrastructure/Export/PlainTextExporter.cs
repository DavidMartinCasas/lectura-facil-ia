// SPDX-License-Identifier: Apache-2.0
using System.Text;
using LecturaFacil.Application.Export;

namespace LecturaFacil.Infrastructure.Export;

/// <summary>
/// Texto plano en UTF-8 (sin BOM), con saltos de línea <c>\n</c>: el título, cada sección con su pregunta y una frase por
/// línea, el glosario y, al final, el aviso de borrador. El texto ya es texto: no hay nada que escapar.
/// </summary>
public sealed class PlainTextExporter : IDocumentExporter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public ExportFormat Format => ExportFormat.PlainText;

    public string ContentType => "text/plain; charset=utf-8";

    public string FileExtension => "txt";

    public byte[] Export(ExportableDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var text = new StringBuilder();
        text.Append(document.Title).Append("\n\n");
        foreach (var section in document.Sections)
        {
            text.Append(section.Question).Append('\n');
            foreach (var sentence in section.Sentences)
            {
                text.Append(sentence).Append('\n');
            }

            text.Append('\n');
        }

        if (document.Glossary.Count > 0)
        {
            text.Append(document.GlossaryTitle).Append('\n');
            foreach (var term in document.Glossary)
            {
                text.Append(term.Term).Append(": ").Append(term.Definition).Append('\n');
            }

            text.Append('\n');
        }

        text.Append(document.Footer).Append('\n');
        return Utf8WithoutBom.GetBytes(text.ToString());
    }
}
