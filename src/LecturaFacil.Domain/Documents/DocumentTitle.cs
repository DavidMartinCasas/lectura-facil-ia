// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>
/// Título de un documento: una sola línea, sin caracteres de control y de <see cref="MaxLength"/> caracteres como mucho.
/// Si el usuario no da un título válido, se usa <see cref="Default"/>.
/// </summary>
public sealed record DocumentTitle
{
    public const int MaxLength = 200;

    private const string PdfExtension = ".pdf";

    private DocumentTitle(string value) => Value = value;

    public static DocumentTitle Default { get; } = new("Documento sin título");

    public string Value { get; }

    /// <summary>Sanea el texto del usuario. Nunca falla: si no queda nada útil, devuelve <see cref="Default"/>.</summary>
    public static DocumentTitle Create(string? value)
    {
        var sanitized = TextSanitizer.Truncate(TextSanitizer.NormalizeSingleLine(value ?? string.Empty), MaxLength);
        return sanitized.Length == 0 ? Default : new DocumentTitle(sanitized);
    }

    /// <summary>Título a partir del nombre de un fichero subido: sin ruta ni extensión. El nombre nunca se usa como ruta.</summary>
    public static DocumentTitle FromFileName(string? fileName)
    {
        var name = fileName ?? string.Empty;
        var lastSeparator = name.LastIndexOfAny(['/', '\\']);
        var withoutPath = name[(lastSeparator + 1)..];
        var withoutExtension = withoutPath.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase)
            ? withoutPath[..^PdfExtension.Length]
            : withoutPath;
        return Create(withoutExtension);
    }

    /// <summary>Reconstruye un título ya guardado tal cual, sin volver a sanearlo.</summary>
    public static DocumentTitle Restore(string storedTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedTitle);
        return new DocumentTitle(storedTitle);
    }

    public override string ToString() => Value;
}
