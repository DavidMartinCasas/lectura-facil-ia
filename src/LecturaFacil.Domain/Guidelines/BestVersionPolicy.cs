// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Entre dos adaptaciones del mismo texto, cuál se guarda: la que tiene menos incumplimientos graves.</summary>
public static class BestVersionPolicy
{
    /// <summary>
    /// <c>true</c> si el reintento es mejor: menos hallazgos de gravedad alta o, con los mismos, menos hallazgos en total.
    /// Si empatan, se queda la primera versión.
    /// </summary>
    public static bool PrefersRetry(GuidelineReport current, GuidelineReport retry)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(retry);

        if (retry.HighSeverityFindingCount != current.HighSeverityFindingCount)
        {
            return retry.HighSeverityFindingCount < current.HighSeverityFindingCount;
        }

        return retry.FindingCount < current.FindingCount;
    }
}
