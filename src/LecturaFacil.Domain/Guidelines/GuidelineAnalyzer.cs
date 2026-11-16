// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Aplica todas las reglas registradas a un texto. Las posiciones de los hallazgos se calculan sobre el texto tal y
/// como se recibe, así que hay que pasarle el texto ya normalizado que se guarda (por ejemplo, <c>OriginalText.Value</c>).
/// </summary>
public sealed class GuidelineAnalyzer
{
    private readonly IReadOnlyList<IGuidelineRule> _rules;

    public GuidelineAnalyzer(IEnumerable<IGuidelineRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = [.. rules];
    }

    public GuidelineAnalysis Analyze(string text)
    {
        var guidelineText = GuidelineText.Create(text);
        var findings = new List<Finding>();
        var timedOut = new List<string>();

        foreach (var rule in _rules)
        {
            try
            {
                findings.AddRange([.. rule.Check(guidelineText)]);
            }
            catch (RegexMatchTimeoutException)
            {
                // Un texto patológico no tumba el análisis: esa pauta queda sin comprobar y se informa.
                timedOut.Add(rule.Guideline.Code);
            }
        }

        var ordered = findings
            .OrderBy(finding => finding.Start)
            .ThenBy(finding => finding.End)
            .ThenBy(finding => finding.GuidelineCode, StringComparer.Ordinal)
            .ToList();
        return new GuidelineAnalysis(ordered, timedOut);
    }
}
