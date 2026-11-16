// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Domain.Guidelines;

/// <summary>Entre dos adaptaciones del mismo texto, cuál se guarda: la que tiene menos incumplimientos graves.</summary>
public static partial class BestVersionPolicy
{
    /// <summary>
    /// <c>true</c> si el reintento es mejor. Nunca lo es si ha perdido alguna cifra de la primera versión (un importe, un
    /// plazo, una fecha, un teléfono…): cumplir pautas quitando datos no es mejor. Si conserva todas, decide el informe.
    /// </summary>
    public static bool PrefersRetry(AdaptedContent current, GuidelineReport currentReport, AdaptedContent retry, GuidelineReport retryReport)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(retry);

        return Figures(current).IsSubsetOf(Figures(retry)) && PrefersRetry(currentReport, retryReport);
    }

    /// <summary>
    /// <c>true</c> si el informe del reintento es mejor: menos hallazgos de gravedad alta o, con los mismos, menos
    /// hallazgos en total. Si empatan, se queda la primera. Una pauta grave que el reintento no ha podido comprobar y la
    /// primera sí nunca cuenta como mejora.
    /// </summary>
    public static bool PrefersRetry(GuidelineReport current, GuidelineReport retry)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(retry);

        if (SevereNotChecked(retry).Except(SevereNotChecked(current), StringComparer.Ordinal).Any())
        {
            return false;
        }

        if (retry.HighSeverityFindingCount != current.HighSeverityFindingCount)
        {
            return retry.HighSeverityFindingCount < current.HighSeverityFindingCount;
        }

        return retry.FindingCount < current.FindingCount;
    }

    private static IEnumerable<string> SevereNotChecked(GuidelineReport report) =>
        report.Results
            .Where(result => result.Status == GuidelineStatus.NotChecked && result.Guideline.DefaultSeverity == Severity.High)
            .Select(result => result.GuidelineCode);

    /// <summary>Las cifras del texto y de las definiciones del glosario ("15", "2026", "2,5", "985 123 456" son 15, 2026, 2,5, 985, 123 y 456).</summary>
    private static HashSet<string> Figures(AdaptedContent content)
    {
        var text = string.Join('\n', content.Glossary.Select(term => term.Definition).Prepend(content.PlainText));
        return FigurePattern().Matches(text).Select(match => match.Value).ToHashSet(StringComparer.Ordinal);
    }

    [GeneratedRegex(@"\p{Nd}+(?:[.,]\p{Nd}+)*", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex FigurePattern();
}
