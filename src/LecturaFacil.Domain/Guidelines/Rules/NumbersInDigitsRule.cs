// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>Números escritos con letras ("quince", "treinta y dos"). El "ciento" de "por ciento" es de la pauta de porcentajes.</summary>
public sealed partial class NumbersInDigitsRule : IGuidelineRule
{
    private const string ForPrefix = "por ";

    private const string NumberWord = "(?:" + SpanishNumberWords.Alternation + ")";

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.NumbersInDigits);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (Match match in NumberWordsPattern().Matches(text.Value))
        {
            if (IsPercentageHundred(text.Value, match))
            {
                continue;
            }

            yield return Finding.At(Guideline, text.Value, match.Index, match.Index + match.Length,
                $"Escribe el número con cifras en lugar de «{match.Value}».");
        }
    }

    private static bool IsPercentageHundred(string text, Match match) =>
        match.Value.ToUpperInvariant() is "CIEN" or "CIENTO"
            && match.Index >= ForPrefix.Length
            && text.AsSpan(match.Index - ForPrefix.Length, ForPrefix.Length).Equals(ForPrefix, StringComparison.OrdinalIgnoreCase)
            && (match.Index == ForPrefix.Length || !char.IsLetter(text[match.Index - ForPrefix.Length - 1]));

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])" + NumberWord + @"(?:\s+(?:y\s+)?" + NumberWord + @")*(?![\p{L}\p{N}])",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex NumberWordsPattern();
}
