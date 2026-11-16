// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text.RegularExpressions;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Encuentra las cifras de un texto en castellano y las normaliza para poder compararlas: "quince" → 15,
/// "250 €" → 250, "1.500" → 1500, "2,5" → 2.5, "40 %" → 40, y "15 de diciembre de 2026" y "15/12/2026" → la misma fecha.
/// Los números con letras se leen hasta los millones; "un", "una" y "uno" sueltos no cuentan porque casi siempre son artículos.
/// </summary>
public static partial class NumberNormalizer
{
    private const string ForPrefix = "por ";
    private const int ThousandValue = 1_000;
    private const int MillionValue = 1_000_000;

    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cero"] = 0,
        ["dos"] = 2,
        ["tres"] = 3,
        ["cuatro"] = 4,
        ["cinco"] = 5,
        ["seis"] = 6,
        ["siete"] = 7,
        ["ocho"] = 8,
        ["nueve"] = 9,
        ["diez"] = 10,
        ["once"] = 11,
        ["doce"] = 12,
        ["trece"] = 13,
        ["catorce"] = 14,
        ["quince"] = 15,
        ["dieciséis"] = 16,
        ["dieciseis"] = 16,
        ["diecisiete"] = 17,
        ["dieciocho"] = 18,
        ["diecinueve"] = 19,
        ["veinte"] = 20,
        ["veintiuno"] = 21,
        ["veintiuna"] = 21,
        ["veintiún"] = 21,
        ["veintidós"] = 22,
        ["veintidos"] = 22,
        ["veintitrés"] = 23,
        ["veintitres"] = 23,
        ["veinticuatro"] = 24,
        ["veinticinco"] = 25,
        ["veintiséis"] = 26,
        ["veintiseis"] = 26,
        ["veintisiete"] = 27,
        ["veintiocho"] = 28,
        ["veintinueve"] = 29,
        ["treinta"] = 30,
        ["cuarenta"] = 40,
        ["cincuenta"] = 50,
        ["sesenta"] = 60,
        ["setenta"] = 70,
        ["ochenta"] = 80,
        ["noventa"] = 90,
        ["cien"] = 100,
        ["ciento"] = 100,
        ["doscientos"] = 200,
        ["doscientas"] = 200,
        ["trescientos"] = 300,
        ["trescientas"] = 300,
        ["cuatrocientos"] = 400,
        ["cuatrocientas"] = 400,
        ["quinientos"] = 500,
        ["quinientas"] = 500,
        ["seiscientos"] = 600,
        ["seiscientas"] = 600,
        ["setecientos"] = 700,
        ["setecientas"] = 700,
        ["ochocientos"] = 800,
        ["ochocientas"] = 800,
        ["novecientos"] = 900,
        ["novecientas"] = 900,
        ["mil"] = ThousandValue,
        ["millón"] = MillionValue,
        ["millones"] = MillionValue,
        ["uno"] = 1,
        ["una"] = 1,
        ["un"] = 1,
    };

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enero"] = 1,
        ["febrero"] = 2,
        ["marzo"] = 3,
        ["abril"] = 4,
        ["mayo"] = 5,
        ["junio"] = 6,
        ["julio"] = 7,
        ["agosto"] = 8,
        ["septiembre"] = 9,
        ["setiembre"] = 9,
        ["octubre"] = 10,
        ["noviembre"] = 11,
        ["diciembre"] = 12,
    };

    /// <summary>Las cifras del texto, en el orden en que aparecen.</summary>
    /// <exception cref="RegexMatchTimeoutException">Si un texto anómalo agota el tiempo de búsqueda (ReDoS).</exception>
    public static IReadOnlyList<NumberMention> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var mentions = new List<NumberMention>();
        AddDates(text, mentions);
        AddNumbers(text, DigitsPattern(), ParseDigits, mentions);
        AddNumbers(text, WordsPattern(), ParseWords, mentions);
        return mentions.OrderBy(mention => mention.Start).ToList().AsReadOnly();
    }

    /// <summary>Los valores distintos de las cifras del texto.</summary>
    public static IReadOnlySet<NumericValue> Values(string? text) =>
        string.IsNullOrWhiteSpace(text) ? new HashSet<NumericValue>() : Find(text).Select(mention => mention.Value).ToHashSet();

    private static void AddDates(string text, List<NumberMention> mentions)
    {
        foreach (Match match in NumericDatePattern().Matches(text))
        {
            if (TryDate(match.Groups["year"].Value, match.Groups["month"].Value, match.Groups["day"].Value) is { } date)
            {
                mentions.Add(new NumberMention(NumericValue.Of(date), match.Value, match.Index));
            }
        }

        foreach (Match match in TextualDatePattern().Matches(text))
        {
            var month = Months[match.Groups["month"].Value];
            var day = ParseDay(match.Groups["day"].Value);
            if (!Overlaps(mentions, match.Index, match.Length)
                && day is not null
                && TryDate(match.Groups["year"].Value, month.ToString(CultureInfo.InvariantCulture), day) is { } date)
            {
                mentions.Add(new NumberMention(NumericValue.Of(date), match.Value, match.Index));
            }
        }
    }

    private static void AddNumbers(string text, Regex pattern, Func<string, decimal?> parse, List<NumberMention> mentions)
    {
        foreach (Match match in pattern.Matches(text))
        {
            var number = match.Groups["number"];
            if (Overlaps(mentions, match.Index, match.Length) || IsPercentageHundred(text, number) || parse(number.Value) is not { } value)
            {
                continue;
            }

            mentions.Add(new NumberMention(NumericValue.Of(value), match.Value, match.Index));
        }
    }

    private static string? ParseDay(string day) =>
        int.TryParse(day, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? day
            : ParseWords(day)?.ToString(CultureInfo.InvariantCulture);

    private static DateOnly? TryDate(string year, string month, string day) =>
        DateOnly.TryParseExact($"{year}-{month.PadLeft(2, '0')}-{day.PadLeft(2, '0')}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static bool Overlaps(List<NumberMention> mentions, int start, int length) =>
        mentions.Exists(mention => start < mention.Start + mention.Length && mention.Start < start + length);

    /// <summary>"1.500,75" → 1500.75: el punto separa los miles y la coma, los decimales.</summary>
    private static decimal? ParseDigits(string digits) =>
        decimal.TryParse(
            digits.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.'),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;

    /// <summary>"dos mil quinientos" → 2500, "treinta y uno" → 31. Un artículo suelto ("un", "una") no es un número.</summary>
    private static decimal? ParseWords(string words)
    {
        var parts = words.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !word.Equals("y", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (parts.Count == 1 && NumberWords.GetValueOrDefault(parts[0]) == 1)
        {
            return null;
        }

        decimal total = 0;
        decimal current = 0;
        foreach (var part in parts)
        {
            if (!NumberWords.TryGetValue(part, out var value))
            {
                return null;
            }

            switch (value)
            {
                case ThousandValue:
                    total += (current == 0 ? 1 : current) * ThousandValue;
                    current = 0;
                    break;
                case MillionValue:
                    total = (total + current == 0 ? 1 : total + current) * MillionValue;
                    current = 0;
                    break;
                default:
                    current += value;
                    break;
            }
        }

        return total + current;
    }

    /// <summary>El "ciento" de "cuarenta por ciento" es la unidad del porcentaje, no el número 100.</summary>
    private static bool IsPercentageHundred(string text, Group number) =>
        number.Value.StartsWith("cien", StringComparison.OrdinalIgnoreCase)
            && number.Index >= ForPrefix.Length
            && text.AsSpan(number.Index - ForPrefix.Length, ForPrefix.Length).Equals(ForPrefix, StringComparison.OrdinalIgnoreCase);

    private const string Unit = @"(?:\s*(?:%|€)|\s+(?:euros?|por\s+ciento)(?![\p{L}\p{N}]))?";

    private const string NumberWord =
        "(?:cero|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|dieciséis|dieciseis|"
        + "diecisiete|dieciocho|diecinueve|veinte|veintiuno|veintiuna|veintiún|veintidós|veintidos|veintitrés|veintitres|"
        + "veinticuatro|veinticinco|veintiséis|veintiseis|veintisiete|veintiocho|veintinueve|treinta|cuarenta|cincuenta|"
        + "sesenta|setenta|ochenta|noventa|cien|ciento|doscientos|doscientas|trescientos|trescientas|cuatrocientos|"
        + "cuatrocientas|quinientos|quinientas|seiscientos|seiscientas|setecientos|setecientas|ochocientos|ochocientas|"
        + "novecientos|novecientas|mil|millón|millones|un|una|uno)";

    private const string MonthName = "(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)";

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?<number>" + NumberWord + @"(?:\s+(?:y\s+)?" + NumberWord + @")*)(?![\p{L}\p{N}])" + Unit,
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex WordsPattern();

    [GeneratedRegex(
        @"(?<![\p{N}.,])(?<number>\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d+(?:,\d+)?)(?![\p{N}])" + Unit,
        GuidelineRegex.Options,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex DigitsPattern();

    [GeneratedRegex(
        @"(?<![\p{N}/-])(?<day>\d{1,2})(?<sep>[/-])(?<month>\d{1,2})\k<sep>(?<year>\d{4})(?![\p{N}])",
        GuidelineRegex.Options,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?<day>\d{1,2}|\p{L}+)\s+de\s+" + MonthName + @"\s+del?\s+(?<year>\d{4})(?![\p{N}])",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex TextualDatePattern();
}
