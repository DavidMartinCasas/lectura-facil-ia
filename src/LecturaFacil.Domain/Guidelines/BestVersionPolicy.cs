// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
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

    /// <summary>
    /// Las cifras del texto y de las definiciones del glosario ("15", "2,5", "985 123 456" son 15, 2,5, 985, 123 y 456).
    /// Las fechas cuentan como una sola cifra con el mismo valor escritas como sean: "15/12/2026" y "15 de diciembre de
    /// 2026" son la misma, para que pasar una fecha a su forma completa (FECHAS_COMPLETAS) no parezca perder un dato.
    /// </summary>
    private static HashSet<string> Figures(AdaptedContent content)
    {
        var text = string.Join('\n', content.Glossary.Select(term => term.Definition).Prepend(content.PlainText));
        var figures = new HashSet<string>(StringComparer.Ordinal);
        text = NumericDatePattern().Replace(text, match => AddDate(figures, match, Number(match.Groups["month"].Value)));
        text = WrittenDatePattern().Replace(text, match => AddDate(figures, match, MonthNumber(match.Groups["month"].Value)));
        figures.UnionWith(FigurePattern().Matches(text).Select(match => match.Value));
        return figures;
    }

    private static string AddDate(HashSet<string> figures, Match date, int month)
    {
        figures.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"{Number(date.Groups["day"].Value)}/{month}/{Number(date.Groups["year"].Value)}"));
        return " ";
    }

    private static int Number(string digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

    private static int MonthNumber(string name) =>
        Array.FindIndex(Months, month => month.Equals(name, StringComparison.OrdinalIgnoreCase)) switch
        {
            < 0 => 9, // "setiembre", la única forma que no está en la lista.
            var index => index + 1,
        };

    private static readonly string[] Months =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?<day>\d{1,2})[/.\-](?<month>\d{1,2})[/.\-](?<year>\d{4})(?![\p{L}\p{N}])", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?<day>\d{1,2})\s+de\s+(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)\s+de(?:l)?\s+(?<year>\d{4})(?![\p{L}\p{N}])",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex WrittenDatePattern();

    [GeneratedRegex(@"\p{Nd}+(?:[.,]\p{Nd}+)*", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex FigurePattern();
}
