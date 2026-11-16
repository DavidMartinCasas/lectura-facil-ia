// SPDX-License-Identifier: Apache-2.0
using PdfSharp.Fonts;

namespace LecturaFacil.Infrastructure.Export;

/// <summary>
/// Da a PDFsharp la fuente Atkinson Hyperlegible (SIL OFL 1.1) incluida en el ensamblado, en regular y negrita. No usa
/// las fuentes del sistema: el PDF es igual en cualquier servidor. Cualquier otra familia que pida MigraDoc (por ejemplo,
/// su fuente para mensajes de error) se resuelve también con Atkinson Hyperlegible. La cursiva se simula sobre la regular.
/// </summary>
internal sealed class EmbeddedFontResolver : IFontResolver
{
    public const string FamilyName = "Atkinson Hyperlegible";

    private const string RegularFace = "AtkinsonHyperlegible-Regular";
    private const string BoldFace = "AtkinsonHyperlegible-Bold";
    private const string ResourcePrefix = "LecturaFacil.Infrastructure.Export.Fonts.";

    /// <summary>PDFsharp pide que se registre siempre la misma instancia.</summary>
    public static EmbeddedFontResolver Instance { get; } = new();

    private EmbeddedFontResolver()
    {
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(bold ? BoldFace : RegularFace, mustSimulateBold: false, mustSimulateItalic: italic);

    public byte[]? GetFont(string faceName)
    {
        if (faceName is not (RegularFace or BoldFace))
        {
            return null;
        }

        using var resource = typeof(EmbeddedFontResolver).Assembly.GetManifestResourceStream($"{ResourcePrefix}{faceName}.ttf")
            ?? throw new InvalidOperationException($"The embedded font {faceName} is missing.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }
}
