// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>
/// Números romanos bien formados en mayúsculas ("IV", "XII", "MMXXVI"). De una sola letra solo cuentan I, V y X:
/// C, D, L y M sueltas suelen ser abreviaturas ("D. Juan", "calle C") o tallas.
/// </summary>
public sealed partial class RomanNumeralsRule : IGuidelineRule
{
    private const string Numeral = "M{0,3}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3})";

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.RomanNumerals);

    public static bool IsRomanNumeral(string value) => !string.IsNullOrEmpty(value) && WholeNumeralPattern().IsMatch(value);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (Match match in NumeralPattern().Matches(text.Value))
        {
            if (match.Length == 1 && match.Value is not ("I" or "V" or "X"))
            {
                continue;
            }

            yield return Finding.At(Guideline, text.Value, match.Index, match.Index + match.Length,
                $"Escribe el número con cifras en lugar de «{match.Value}» (por ejemplo, «capítulo 3» en lugar de «capítulo III»).");
        }
    }

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?=[MDCLXVI])" + Numeral + @"(?![\p{L}\p{N}])",
        GuidelineRegex.Options,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex NumeralPattern();

    [GeneratedRegex(@"^(?=[MDCLXVI])" + Numeral + "$", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex WholeNumeralPattern();
}
