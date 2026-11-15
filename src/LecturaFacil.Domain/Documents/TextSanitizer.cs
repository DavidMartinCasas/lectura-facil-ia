// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text;

namespace LecturaFacil.Domain.Documents;

/// <summary>Limpieza de texto que llega del usuario o de un PDF: Unicode NFC y sin caracteres de control.</summary>
internal static class TextSanitizer
{
    /// <summary>NFC, saltos de línea como <c>\n</c>, tabuladores como espacios y sin ningún otro carácter de control.</summary>
    public static string NormalizeKeepingLineBreaks(string value)
    {
        var unified = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace('\t', ' ');
        return RemoveControlCharacters(unified, keepLineFeeds: true).Normalize(NormalizationForm.FormC);
    }

    /// <summary>NFC, en una sola línea y con los espacios repetidos reducidos a uno.</summary>
    public static string NormalizeSingleLine(string value)
    {
        var withoutControls = RemoveControlCharacters(value, keepLineFeeds: false, replacement: ' ');
        var words = withoutControls.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', words).Normalize(NormalizationForm.FormC);
    }

    /// <summary>Corta a <paramref name="maxLength"/> sin dejar medio par sustituto al final.</summary>
    public static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length].TrimEnd();
    }

    private static string RemoveControlCharacters(string value, bool keepLineFeeds, char? replacement = null)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!IsControl(character) || (keepLineFeeds && character == '\n'))
            {
                builder.Append(character);
            }
            else if (replacement is { } substitute && char.IsWhiteSpace(character))
            {
                builder.Append(substitute);
            }
        }

        return builder.ToString();
    }

    private static bool IsControl(char character) =>
        char.GetUnicodeCategory(character) is UnicodeCategory.Control or UnicodeCategory.Format
            && !IsAllowedFormatCharacter(character);

    // Unión de palabras (ZWJ) y no unión (ZWNJ): necesarias en emojis y en algunas escrituras.
    private static bool IsAllowedFormatCharacter(char character) => character is '‌' or '‍';
}
