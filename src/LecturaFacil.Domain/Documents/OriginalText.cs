// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Documents;

/// <summary>
/// Texto original de un documento, normalizado (Unicode NFC, saltos de línea <c>\n</c> y sin caracteres de control).
/// Su longitud, tras normalizar, está dentro de los <see cref="TextLengthLimits"/> configurados.
/// </summary>
public sealed record OriginalText
{
    private OriginalText(string value) => Value = value;

    public string Value { get; }

    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TextSanitizer.NormalizeKeepingLineBreaks(text).Trim();
    }

    /// <summary>Comprueba la longitud del texto una vez normalizado.</summary>
    public static TextLengthCheck CheckLength(string text, TextLengthLimits limits)
    {
        var length = Normalize(text).Length;
        if (length < limits.Minimum)
        {
            return TextLengthCheck.TooShort;
        }

        return length > limits.Maximum ? TextLengthCheck.TooLong : TextLengthCheck.Valid;
    }

    /// <summary>Normaliza el texto. Lanza si su longitud está fuera de los límites: compruébala antes con <see cref="CheckLength"/>.</summary>
    public static OriginalText Create(string text, TextLengthLimits limits)
    {
        var check = CheckLength(text, limits);
        if (check != TextLengthCheck.Valid)
        {
            throw new ArgumentException($"The text length is not valid: {check}.", nameof(text));
        }

        return new OriginalText(Normalize(text));
    }

    /// <summary>
    /// Reconstruye un texto ya guardado. No vuelve a aplicar los límites, que son configurables y pueden haber cambiado
    /// desde que se creó el documento.
    /// </summary>
    public static OriginalText Restore(string storedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedText);
        return new OriginalText(storedText);
    }

    public override string ToString() => Value;
}
