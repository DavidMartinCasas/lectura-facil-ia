// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Las siglas y las palabras difíciles que aparecen en el glosario de la adaptación están explicadas: sus hallazgos no
/// cuentan. Un término explica un fragmento si es igual a él, sin distinguir mayúsculas, o si tiene la forma
/// "Expansión (SIGLA)" o "SIGLA (Expansión)" y una de sus dos partes es igual al fragmento. Así "BOP" lo explican "BOP"
/// y "Boletín Oficial de la Provincia (BOP)", pero no "BOPA" ni un término que junta varias siglas ("BOP IPREM").
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
        var explained = glossaryTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .SelectMany(Forms)
            .Select(Key)
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (explained.Count == 0)
        {
            return findings;
        }

        return findings
            .Where(finding => !ExplainableGuidelines.Contains(finding.GuidelineCode) || !explained.Contains(Key(finding.Fragment)))
            .ToList();
    }

    /// <summary>El término entero y, si lleva un paréntesis, lo que hay antes y lo que hay dentro.</summary>
    private static IEnumerable<string> Forms(string term)
    {
        yield return term;

        var open = term.IndexOf('(', StringComparison.Ordinal);
        var close = term.LastIndexOf(')');
        if (open < 0 || close < open)
        {
            yield break;
        }

        yield return term[..open];
        yield return term[(open + 1)..close];
    }

    private static string Key(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
