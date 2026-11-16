// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>
/// Siglas (palabras de 2 a 6 mayúsculas) sin explicar. Una sigla está explicada si alguna vez aparece entre paréntesis
/// o seguida de un paréntesis: "Boletín Oficial de la Provincia (BOP)" o "BOP (Boletín Oficial…)". No cuentan las
/// siglas permitidas, los números romanos (tienen su propia pauta) ni las palabras de títulos escritos en mayúsculas
/// ("BASES DE LA CONVOCATORIA"): una frase es un título si más de la mitad de sus palabras están en mayúsculas.
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

        var headings = text.Sentences.Where(IsUppercaseHeading).ToList();
        var candidates = AcronymPattern().Matches(text.Value)
            .Where(match => !_allowed.Contains(match.Value) && !RomanNumeralsRule.IsRomanNumeral(match.Value))
            .Where(match => !IsInside(headings, match.Index))
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

    private static bool IsUppercaseHeading(Sentence sentence)
    {
        var words = LetterWordPattern().Matches(sentence.Text);
        var uppercase = words.Count(word => word.Value.Length > 1 && word.Value.All(char.IsUpper));
        return uppercase * 2 > words.Count;
    }

    /// <summary>Las frases están ordenadas por posición: búsqueda binaria.</summary>
    private static bool IsInside(List<Sentence> sentences, int index)
    {
        var low = 0;
        var high = sentences.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var sentence = sentences[middle];
            if (index < sentence.Start)
            {
                high = middle - 1;
            }
            else if (index >= sentence.End)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExplainedAt(string text, Match match)
    {
        var end = match.Index + match.Length;
        var insideParentheses = match.Index > 0 && text[match.Index - 1] == '(' && end < text.Length && text[end] == ')';
        return insideParentheses || text.AsSpan(end).TrimStart(' ').StartsWith("(", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])[A-ZÁÉÍÓÚÜÑ]{2,6}(?![\p{L}\p{N}])", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex AcronymPattern();

    [GeneratedRegex(@"\p{L}+", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex LetterWordPattern();
}
