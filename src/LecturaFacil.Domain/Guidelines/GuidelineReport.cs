// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Informe de pautas de un texto adaptado: para cada pauta del <see cref="GuidelineCatalog"/>, si se cumple y sus
/// hallazgos. Las siglas y las palabras difíciles que están en el glosario cuentan como explicadas.
/// </summary>
public sealed class GuidelineReport
{
    private GuidelineReport(IReadOnlyList<GuidelineResult> results) => Results = results;

    /// <summary>Una entrada por pauta, en el orden del catálogo.</summary>
    public IReadOnlyList<GuidelineResult> Results { get; }

    public int TotalCount => Results.Count;

    public int CompliantCount => Results.Count(result => result.Status == GuidelineStatus.Compliant);

    public int FindingCount => Results.Sum(result => result.FindingCount);

    /// <summary>Hallazgos de las pautas de gravedad alta: los que deciden el reintento y la mejor versión.</summary>
    public int HighSeverityFindingCount => Results
        .Where(result => result.Guideline.DefaultSeverity == Severity.High)
        .Sum(result => result.FindingCount);

    public bool HasHighSeverityViolations => HighSeverityFindingCount > 0;

    /// <summary>Pautas incumplidas, de la más a la menos grave y, a igual gravedad, de más a menos hallazgos.</summary>
    public IReadOnlyList<GuidelineResult> Violations => Results
        .Where(result => result.Status == GuidelineStatus.NotCompliant)
        .OrderByDescending(result => result.Guideline.DefaultSeverity)
        .ThenByDescending(result => result.FindingCount)
        .ToList()
        .AsReadOnly();

    /// <summary>Por ejemplo, "12 de 12 pautas cumplidas".</summary>
    public string Summary => string.Create(CultureInfo.InvariantCulture, $"{CompliantCount} de {TotalCount} pautas cumplidas");

    /// <summary>
    /// Informe de una adaptación: los hallazgos de las reglas de texto sobre su <see cref="AdaptedContent.PlainText"/>,
    /// más los de <see cref="ClearHeadingsRule"/>, que necesita la estructura de secciones. El glosario explica términos.
    /// </summary>
    /// <param name="textAnalysis">Análisis de <see cref="AdaptedContent.PlainText"/> con las reglas de texto.</param>
    public static GuidelineReport ForAdaptation(
        AdaptedContent content,
        GuidelineAnalysis textAnalysis,
        int maxWordsPerHeading,
        int maxStoredFindings)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(textAnalysis);

        var findings = textAnalysis.Findings
            .Concat(new ClearHeadingsRule(maxWordsPerHeading).Check(content))
            .OrderBy(finding => finding.Start)
            .ThenBy(finding => finding.End)
            .ThenBy(finding => finding.GuidelineCode, StringComparer.Ordinal)
            .ToList();
        return Create(
            textAnalysis with { Findings = findings },
            content.Glossary.Select(term => term.Term),
            maxStoredFindings);
    }

    /// <param name="analysis">Análisis del texto. Las pautas sin hallazgos se dan por cumplidas.</param>
    /// <param name="glossaryTerms">Términos del glosario de la adaptación.</param>
    /// <param name="maxStoredFindings">Hallazgos que se guardan como máximo (los primeros del texto); los recuentos son siempre reales.</param>
    public static GuidelineReport Create(GuidelineAnalysis analysis, IEnumerable<string> glossaryTerms, int maxStoredFindings)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(glossaryTerms);
        ArgumentOutOfRangeException.ThrowIfNegative(maxStoredFindings);

        var findings = ExplainedTerms.Exclude(analysis.Findings, glossaryTerms);
        var stored = findings.Take(maxStoredFindings).ToHashSet();
        var notChecked = analysis.TimedOutGuidelines.ToHashSet(StringComparer.Ordinal);
        var results = GuidelineCatalog.All.Select(guideline =>
        {
            var own = findings.Where(finding => finding.GuidelineCode == guideline.Code).ToList();
            var status = notChecked.Contains(guideline.Code) ? GuidelineStatus.NotChecked
                : own.Count > 0 ? GuidelineStatus.NotCompliant
                : GuidelineStatus.Compliant;
            return new GuidelineResult(guideline.Code, status, own.Count, own.Where(stored.Contains).ToList().AsReadOnly());
        });
        return new GuidelineReport(results.ToList().AsReadOnly());
    }

    /// <summary>Reconstruye un informe ya guardado.</summary>
    public static GuidelineReport Restore(IEnumerable<GuidelineResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return new GuidelineReport(results.ToList().AsReadOnly());
    }

    public GuidelineResult Find(string guidelineCode) =>
        Results.FirstOrDefault(result => result.GuidelineCode == guidelineCode)
        ?? throw new KeyNotFoundException($"The report has no result for {guidelineCode}.");
}
