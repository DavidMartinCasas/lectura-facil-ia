// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>
/// Fechas en cifras: "15/12/2026", "1-3-26", "05.06.2026" o "2026-12-15". Con puntos se exige el año con cuatro cifras
/// para no confundir una fecha con un apartado ("3.2.10"). Se sugiere "15 de diciembre de 2026".
/// </summary>
public sealed partial class NumericDateRule : IGuidelineRule
{
    private const int MonthsInYear = 12;
    private const int MaxDayOfMonth = 31;
    private const int FullYearDigits = 4;

    private static readonly string[] MonthNames =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.NumericDate);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (Match match in DatePattern().Matches(text.Value))
        {
            var day = Number(match, "day", "isoday");
            var month = Number(match, "month", "isomonth");
            if (day is < 1 or > MaxDayOfMonth || month is < 1 or > MonthsInYear)
            {
                continue;
            }

            yield return Finding.At(Guideline, text.Value, match.Index, match.Index + match.Length,
                Suggestion(day, month, Year(match)));
        }
    }

    private static int Number(Match match, string group, string isoGroup)
    {
        var value = match.Groups[group].Success ? match.Groups[group].Value : match.Groups[isoGroup].Value;
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string Year(Match match) =>
        match.Groups["year"].Success ? match.Groups["year"].Value : match.Groups["isoyear"].Value;

    private static string Suggestion(int day, int month, string year)
    {
        var date = string.Create(CultureInfo.InvariantCulture, $"{day} de {MonthNames[month - 1]}");
        return year.Length == FullYearDigits
            ? $"Escribe la fecha completa: «{date} de {year}»."
            : $"Escribe la fecha completa: «{date}» y el año con sus cuatro cifras.";
    }

    [GeneratedRegex(
        @"(?<![\p{N}./-])(?:(?<day>\d{1,2})(?<sep>[/-])(?<month>\d{1,2})\k<sep>(?<year>\d{4}|\d{2})|(?<day>\d{1,2})\.(?<month>\d{1,2})\.(?<year>\d{4})|(?<isoyear>\d{4})-(?<isomonth>\d{2})-(?<isoday>\d{2}))(?![\p{N}]|[./-]\p{N})",
        GuidelineRegex.Options,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex DatePattern();
}
