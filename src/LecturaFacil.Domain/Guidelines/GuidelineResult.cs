// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Resultado de una pauta en un <see cref="GuidelineReport"/>.</summary>
/// <param name="FindingCount">Hallazgos de la pauta en el texto, aunque no todos se guarden.</param>
/// <param name="Findings">Los hallazgos guardados, ordenados por posición (como mucho el límite del informe).</param>
public sealed record GuidelineResult(string GuidelineCode, GuidelineStatus Status, int FindingCount, IReadOnlyList<Finding> Findings)
{
    public Guideline Guideline => GuidelineCatalog.Find(GuidelineCode);

    public bool Equals(GuidelineResult? other) =>
        other is not null
        && GuidelineCode == other.GuidelineCode
        && Status == other.Status
        && FindingCount == other.FindingCount
        && Findings.SequenceEqual(other.Findings);

    public override int GetHashCode() => HashCode.Combine(GuidelineCode, Status, FindingCount, Findings.Count);
}
