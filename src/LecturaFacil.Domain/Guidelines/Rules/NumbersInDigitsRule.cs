// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>Números escritos con letras ("quince", "treinta y dos"). El "ciento" de "por ciento" es de la pauta de porcentajes.</summary>
public sealed partial class NumbersInDigitsRule : IGuidelineRule
{
    private const string NumberWord = "(?:" + SpanishNumberWords.Alternation + ")";

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.NumbersInDigits);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (Match match in NumberWordsPattern().Matches(text.Value))
        {
            yield return Finding.At(Guideline, text.Value, match.Index, match.Index + match.Length,
                $"Escribe el número con cifras en lugar de «{match.Value}».");
        }
    }

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?<!(?<![\p{L}])por\s)" + NumberWord + @"(?:\s+(?:y\s+)?" + NumberWord + @")*(?![\p{L}\p{N}])",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex NumberWordsPattern();
}
