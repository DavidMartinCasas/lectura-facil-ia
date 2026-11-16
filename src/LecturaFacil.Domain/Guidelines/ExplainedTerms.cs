// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Las siglas y las palabras difíciles que aparecen en el glosario de la adaptación están explicadas: sus hallazgos no
/// cuentan. Un término explica un fragmento si lo contiene como palabra o expresión completa, sin distinguir mayúsculas
/// ("BOP" lo explican "BOP" y "Boletín Oficial de la Provincia (BOP)", pero no "BOPA").
/// </summary>
internal static class ExplainedTerms
{
    private static readonly HashSet<string> ExplainableGuidelines = new(StringComparer.Ordinal)
    {
        GuidelineCodes.Acronyms,
        GuidelineCodes.DifficultWords,
    };

    public static IReadOnlyList<Finding> Exclude(IReadOnlyList<Finding> findings, IEnumerable<string> glossaryTerms)
    {
        var terms = glossaryTerms.Where(term => !string.IsNullOrWhiteSpace(term)).ToList();
        if (terms.Count == 0)
        {
            return findings;
        }

        var explained = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        return findings
            .Where(finding => !ExplainableGuidelines.Contains(finding.GuidelineCode)
                || !IsExplained(finding.Fragment, terms, explained))
            .ToList();
    }

    private static bool IsExplained(string fragment, List<string> terms, Dictionary<string, bool> cache)
    {
        if (!cache.TryGetValue(fragment, out var isExplained))
        {
            var fragmentList = new TermList([new TermSuggestion(fragment, string.Empty)]);
            isExplained = terms.Exists(term => fragmentList.Find(term).Any());
            cache[fragment] = isExplained;
        }

        return isExplained;
    }
}
