// SPDX-License-Identifier: Apache-2.0
using System.Text;
using LecturaFacil.Application.Export;

namespace LecturaFacil.Infrastructure.Export;

/// <summary>
/// Markdown (CommonMark/GFM) en UTF-8: el título como <c>#</c>, cada pregunta como <c>##</c>, una frase por párrafo, el
/// glosario como lista y, al final, el aviso de borrador. Todo el texto pasa por <see cref="MarkdownText"/>: un enlace,
/// una imagen o un HTML que haya escrito el modelo aparece como texto literal y nunca se activa (OWASP LLM05).
/// </summary>
public sealed class MarkdownExporter : IDocumentExporter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public ExportFormat Format => ExportFormat.Markdown;

    public string ContentType => "text/markdown; charset=utf-8";

    public string FileExtension => "md";

    public byte[] Export(ExportableDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var markdown = new StringBuilder();
        markdown.Append("# ").Append(MarkdownText.Escape(document.Title)).Append("\n\n");
        foreach (var section in document.Sections)
        {
            markdown.Append("## ").Append(MarkdownText.Escape(section.Question)).Append("\n\n");
            foreach (var sentence in section.Sentences)
            {
                markdown.Append(MarkdownText.Escape(sentence)).Append("\n\n");
            }
        }

        if (document.Glossary.Count > 0)
        {
            markdown.Append("## ").Append(MarkdownText.Escape(document.GlossaryTitle)).Append("\n\n");
            foreach (var term in document.Glossary)
            {
                markdown.Append("- **").Append(MarkdownText.Escape(term.Term)).Append("**: ")
                    .Append(MarkdownText.Escape(term.Definition)).Append('\n');
            }

            markdown.Append('\n');
        }

        markdown.Append("---\n\n*").Append(MarkdownText.Escape(document.Footer)).Append("*\n");
        return Utf8WithoutBom.GetBytes(markdown.ToString());
    }
}
