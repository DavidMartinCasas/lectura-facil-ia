// SPDX-License-Identifier: Apache-2.0
using System.Text;
using System.Text.RegularExpressions;

namespace LecturaFacil.Infrastructure.Export;

/// <summary>
/// Convierte una línea de texto plano en Markdown que se muestra tal cual. Se escapan con <c>\</c> los caracteres que
/// abren sintaxis en línea (énfasis, código, enlaces, imágenes, HTML, entidades, tablas, tachado) y los que al principio
/// de la línea abren un bloque (título, cita, lista). Las direcciones web y de correo van en código, porque los visores
/// GFM convierten en enlace una dirección suelta aunque sus caracteres estén escapados.
/// </summary>
internal static partial class MarkdownText
{
    private const string InlineSpecialCharacters = "\\`*_[]<>|~&";
    private const string BlockStartCharacters = "#+-=>";

    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var escaped = new StringBuilder(text.Length * 2);
        var position = 0;
        foreach (Match address in AddressPattern().Matches(text))
        {
            EscapeInline(text.AsSpan(position, address.Index - position), escaped);
            AppendCodeSpan(address.Value, escaped);
            position = address.Index + address.Length;
        }

        EscapeInline(text.AsSpan(position), escaped);
        return EscapeBlockStart(escaped.ToString());
    }

    private static void EscapeInline(ReadOnlySpan<char> text, StringBuilder escaped)
    {
        foreach (var character in text)
        {
            if (InlineSpecialCharacters.Contains(character, StringComparison.Ordinal))
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }
    }

    /// <summary>Al principio de la línea, <c>#</c>, <c>&gt;</c>, <c>-</c>, <c>+</c> o <c>1.</c> abrirían un título, una cita o una lista.</summary>
    private static string EscapeBlockStart(string line)
    {
        if (line.Length > 0 && BlockStartCharacters.Contains(line[0], StringComparison.Ordinal))
        {
            return "\\" + line;
        }

        var orderedList = OrderedListPattern().Match(line);
        return orderedList.Success ? line.Insert(orderedList.Groups["marker"].Index, "\\") : line;
    }

    /// <summary>Código en línea con una valla más larga que cualquier racha de comillas invertidas del contenido.</summary>
    private static void AppendCodeSpan(string content, StringBuilder escaped)
    {
        var fence = new string('`', LongestBacktickRun(content) + 1);
        var padding = content.StartsWith('`') || content.EndsWith('`') ? " " : string.Empty;
        escaped.Append(fence).Append(padding).Append(content).Append(padding).Append(fence);
    }

    private static int LongestBacktickRun(string content)
    {
        int longest = 0, current = 0;
        foreach (var character in content)
        {
            current = character == '`' ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }

    // Se reconoce de más antes que de menos: la dirección puede ir pegada a cualquier cosa que no sea una letra ASCII
    // ("_www.", "1https://", "éhttps://"…), igual que los autoenlaces GFM. La puntuación final (".", ")"…) se queda fuera,
    // como hacen los visores.
    [GeneratedRegex(@"(?<![A-Za-z])(?:(?:https?|ftp)://|www\.)\S+?(?=[.,;:!?)\]]*(?:\s|$))|[\p{L}\p{N}._%+-]+@[\p{L}\p{N}-]+(?:\.[\p{L}\p{N}-]+)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AddressPattern();

    [GeneratedRegex(@"^\d{1,9}(?<marker>[.)])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OrderedListPattern();
}
