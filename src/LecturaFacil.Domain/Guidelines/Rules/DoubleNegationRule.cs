// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>"no" seguido, en la misma frase, de otra negación: "no … nunca", "no … nada", "no … ningún", "no … nadie".</summary>
public sealed partial class DoubleNegationRule : IGuidelineRule
{
    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.DoubleNegation);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var sentence in text.Sentences)
        {
            int? firstNo = null;
            foreach (Match match in NegationPattern().Matches(sentence.Text))
            {
                if (match.Value.Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    firstNo ??= match.Index;
                }
                else if (firstNo is { } start)
                {
                    yield return Finding.At(Guideline, text.Value, sentence.Start + start, sentence.Start + match.Index + match.Length,
                        "Evita la doble negación. Escribe la frase en positivo (por ejemplo, «siempre» en lugar de «no… nunca»).");
                    firstNo = null;
                }
            }
        }
    }

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?:no|nunca|nada|nadie|ningún|ninguno|ninguna|ningunos|ningunas|tampoco|jamás)(?![\p{L}\p{N}])",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex NegationPattern();
}
