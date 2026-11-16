// SPDX-License-Identifier: Apache-2.0
using System.Globalization;

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

    /// <summary>Por ejemplo, "11 de 11 pautas cumplidas".</summary>
    public string Summary => string.Create(CultureInfo.InvariantCulture, $"{CompliantCount} de {TotalCount} pautas cumplidas");

    /// <param name="analysis">Análisis del texto adaptado.</param>
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
