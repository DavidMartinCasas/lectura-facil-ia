// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Busca palabras o expresiones completas de una lista, sin distinguir mayúsculas y aunque tengan varios espacios o un
/// salto de línea entre palabras. Si dos se solapan, gana la más larga. Una sola expresión regular: coste lineal.
/// </summary>
internal sealed class TermList
{
    private readonly Regex? _pattern;
    private readonly Dictionary<string, string> _alternatives = new(StringComparer.Ordinal);

    public TermList(IEnumerable<TermSuggestion> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        foreach (var term in terms)
        {
            ArgumentNullException.ThrowIfNull(term);
            ArgumentException.ThrowIfNullOrWhiteSpace(term.Term);
            _alternatives.TryAdd(Key(term.Term), term.Alternative?.Trim() ?? string.Empty);
        }

        if (_alternatives.Count == 0)
        {
            return;
        }

        var alternation = string.Join('|', _alternatives.Keys.OrderByDescending(key => key.Length).Select(ToPattern));
        _pattern = new Regex(
            $@"(?<![\p{{L}}\p{{N}}])(?:{alternation})(?![\p{{L}}\p{{N}}])",
            GuidelineRegex.IgnoreCase,
            GuidelineRegex.Timeout);
    }

    /// <summary>Cada aparición con su alternativa (vacía si la lista no da ninguna).</summary>
    public IEnumerable<(Match Match, string Alternative)> Find(string text)
    {
        if (_pattern is null)
        {
            yield break;
        }

        foreach (Match match in _pattern.Matches(text))
        {
            yield return (match, _alternatives.GetValueOrDefault(Key(match.Value), string.Empty));
        }
    }

    private static string Key(string term) =>
        string.Join(' ', term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string ToPattern(string key) => string.Join(@"\s+", key.Split(' ').Select(Regex.Escape));
}
