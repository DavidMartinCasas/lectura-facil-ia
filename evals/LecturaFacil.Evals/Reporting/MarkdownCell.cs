// SPDX-License-Identifier: Apache-2.0
using System.Text;

namespace LecturaFacil.Evals.Reporting;

/// <summary>Texto de una celda de tabla Markdown que se muestra tal cual: en una línea y sin sintaxis que se interprete.</summary>
internal static class MarkdownCell
{
    private const string SpecialCharacters = "\\`*_[]<>|~&!#";

    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var escaped = new StringBuilder(text.Length * 2);
        foreach (var character in text)
        {
            if (char.IsControl(character))
            {
                escaped.Append(' ');
                continue;
            }

            if (SpecialCharacters.Contains(character, StringComparison.Ordinal))
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return escaped.ToString();
    }
}
