// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>
/// Siglas (palabras de 2 a 6 mayúsculas) sin explicar. Una sigla está explicada si alguna vez aparece entre paréntesis
/// o seguida de un paréntesis: "Boletín Oficial de la Provincia (BOP)" o "BOP (Boletín Oficial…)". No cuentan las
/// siglas permitidas ni los números romanos, que tienen su propia pauta.
/// </summary>
public sealed partial class AcronymsRule : IGuidelineRule
{
    private readonly HashSet<string> _allowed;

    public AcronymsRule(IEnumerable<string> allowedAcronyms)
    {
        ArgumentNullException.ThrowIfNull(allowedAcronyms);
        _allowed = new HashSet<string>(allowedAcronyms, StringComparer.Ordinal);
    }

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.Acronyms);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var candidates = AcronymPattern().Matches(text.Value)
            .Where(match => !_allowed.Contains(match.Value) && !RomanNumeralsRule.IsRomanNumeral(match.Value))
            .ToList();
        var explained = candidates
            .Where(match => IsExplainedAt(text.Value, match))
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

        return candidates
            .Where(match => !explained.Contains(match.Value))
            .Select(match => Finding.At(Guideline, text.Value, match.Index, match.Index + match.Length,
                $"Explica qué significa «{match.Value}» la primera vez que aparece o escribe el nombre completo."));
    }

    private static bool IsExplainedAt(string text, Match match)
    {
        var end = match.Index + match.Length;
        var insideParentheses = match.Index > 0 && text[match.Index - 1] == '(' && end < text.Length && text[end] == ')';
        return insideParentheses || text.AsSpan(end).TrimStart(' ').StartsWith("(", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])[A-ZÁÉÍÓÚÜÑ]{2,6}(?![\p{L}\p{N}])", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex AcronymPattern();
}
