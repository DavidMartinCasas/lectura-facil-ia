// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>Porcentajes: "40 %", "40%", "12,5 %", "40 por ciento", "cuarenta por ciento", "100 por cien".</summary>
public sealed partial class PercentagesRule : IGuidelineRule
{
    private const string Number = @"(?:\d+(?:[.,]\d+)?|(?:" + SpanishNumberWords.Alternation + @")(?:\s+y\s+(?:" + SpanishNumberWords.Alternation + @"))?)";

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.Percentages);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (Match match in PercentagePattern().Matches(text.Value))
        {
            yield return Finding.At(Guideline, text.Value, match.Index, match.Index + match.Length,
                "Evita los porcentajes. Explícalo con cantidades o con un ejemplo (por ejemplo, «4 de cada 10 personas»).");
        }
    }

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?:\d+(?:[.,]\d+)?[   ]?%|" + Number + @"\s+por\s+cien(?:to)?(?![\p{L}\p{N}]))",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex PercentagePattern();
}
