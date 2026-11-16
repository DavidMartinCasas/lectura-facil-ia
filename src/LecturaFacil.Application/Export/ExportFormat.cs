// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics.CodeAnalysis;

namespace LecturaFacil.Application.Export;

/// <summary>Formatos en los que se puede descargar una adaptación (US-14).</summary>
public enum ExportFormat
{
    /// <summary>Para seguir editando en Word o LibreOffice. Es el formato principal.</summary>
    Docx,

    /// <summary>Para enviar a validación o publicar tal cual.</summary>
    Pdf,

    /// <summary>Para publicar en webs, wikis o repositorios.</summary>
    Markdown,

    /// <summary>Para copiar en cualquier sitio o integrar con otros sistemas.</summary>
    PlainText,
}

/// <summary>Lista blanca de los nombres de formato que acepta la API. Cualquier otro valor se rechaza.</summary>
public static class ExportFormats
{
    private static readonly Dictionary<string, ExportFormat> FormatsByName = new(StringComparer.Ordinal)
    {
        ["docx"] = ExportFormat.Docx,
        ["pdf"] = ExportFormat.Pdf,
        ["md"] = ExportFormat.Markdown,
        ["txt"] = ExportFormat.PlainText,
    };

    /// <summary>Los nombres admitidos, en minúsculas y exactos: <c>docx</c>, <c>pdf</c>, <c>md</c> y <c>txt</c>.</summary>
    public static IReadOnlyCollection<string> Names => FormatsByName.Keys;

    public static bool TryParse([NotNullWhen(true)] string? name, out ExportFormat format) =>
        FormatsByName.TryGetValue(name ?? string.Empty, out format);
}
