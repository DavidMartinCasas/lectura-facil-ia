// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines;

/// <summary>Palabras de una frase: letras o cifras, con separadores internos ("2,5", "7/2024", "d'Ors"). Los símbolos sueltos no cuentan.</summary>
internal static partial class Words
{
    public static int Count(string text) => WordPattern().Count(text);

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:[.,'’/\-][\p{L}\p{N}]+)*", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex WordPattern();
}
