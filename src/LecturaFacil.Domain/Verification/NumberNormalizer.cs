// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Text.RegularExpressions;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Encuentra las cifras de un texto en castellano y las normaliza para poder compararlas: "quince" → 15,
/// "250 €" → 250, "1.500" y "1 500" → 1500, "15 mil" → 15000, "2,5" → 2.5, "40 %" y "40 por 100" → 40, y
/// "15 de diciembre de 2026", "15/12/2026" y "15.12.26" → la misma fecha ("15 de diciembre", sin año, guarda el día y el mes).
/// Los números con letras se leen hasta los millones. "Un" y "una" sueltos solo cuentan delante de una unidad de tiempo
/// ("un mes"); si no, casi siempre son artículos. La "y" solo une decenas y unidades: "diez y quince" son dos cifras.
/// </summary>
public static partial class NumberNormalizer
{
    private const string ForPrefix = "por ";
    private const int TwoDigitYearBase = 2000;
    private const int LeapYear = 2000;
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
        var taken = new bool[text.Length];
        AddDates(text, mentions, taken);
        AddDigits(text, mentions, taken);
        AddWords(text, mentions, taken);
        return mentions.OrderBy(mention => mention.Start).ToList().AsReadOnly();
    }

    /// <summary>Los valores distintos de las cifras del texto.</summary>
    public static IReadOnlySet<NumericValue> Values(string? text) =>
        string.IsNullOrWhiteSpace(text) ? new HashSet<NumericValue>() : Find(text).Select(mention => mention.Value).ToHashSet();

    private static void AddDates(string text, List<NumberMention> mentions, bool[] taken)
    {
        foreach (Match match in NumericDatePattern().Matches(text))
        {
            if (TryDate(FullYear(match.Groups["year"].Value), match.Groups["month"].Value, match.Groups["day"].Value) is { } date)
            {
                Add(mentions, taken, new NumberMention(NumericValue.Of(date), match.Value, match.Index));
            }
        }

        foreach (Match match in TextualDatePattern().Matches(text))
        {
            var month = Months[match.Groups["month"].Value];
            var day = ParseDay(match.Groups["day"].Value);
            if (Overlaps(taken, match.Index, match.Length) || day is null)
            {
                continue;
            }

            var year = match.Groups["year"];
            var monthText = month.ToString(CultureInfo.InvariantCulture);
            var value = year.Success
                ? TryDate(year.Value, monthText, day) is { } date ? NumericValue.Of(date) : (NumericValue?)null
                : TryDate(LeapYear.ToString(CultureInfo.InvariantCulture), monthText, day) is not null
                    ? NumericValue.OfMonthDay(month, int.Parse(day, CultureInfo.InvariantCulture))
                    : null;
            if (value is { } found)
            {
                Add(mentions, taken, new NumberMention(found, match.Value, match.Index));
            }
        }
    }

    private static void AddDigits(string text, List<NumberMention> mentions, bool[] taken)
    {
        foreach (Match match in DigitsPattern().Matches(text))
        {
            if (Overlaps(taken, match.Index, match.Length) || ParseDigits(match.Groups["number"].Value) is not { } value)
            {
                continue;
            }

            var scale = match.Groups["scale"];
            var multiplier = scale.Success ? NumberWords[scale.Value] : 1;
            Add(mentions, taken, new NumberMention(NumericValue.Of(value * multiplier), match.Value, match.Index));
        }
    }

    private static void AddWords(string text, List<NumberMention> mentions, bool[] taken)
    {
        foreach (Match match in WordsPattern().Matches(text))
        {
            var number = match.Groups["number"];
            if (Overlaps(taken, match.Index, match.Length) || IsPercentageHundred(text, number))
            {
                continue;
            }

            var segments = Segments(number.Value);
            for (var i = 0; i < segments.Count; i++)
            {
                var (start, length) = segments[i];
                var isLast = i == segments.Count - 1;
                var segmentText = number.Value.Substring(start, length);
                var mentionLength = isLast ? match.Index + match.Length - (number.Index + start) : length;
                var value = ParseWords(segmentText)
                    ?? (IsArticleBeforeTimeUnit(segmentText, text, number.Index + start + length) ? 1 : null);
                if (value is { } found)
                {
                    Add(mentions, taken, new NumberMention(NumericValue.Of(found), text.Substring(number.Index + start, mentionLength), number.Index + start));
                }
            }
        }
    }

    /// <summary>
    /// Parte una secuencia de palabras numéricas por cada "y" que no une una decena con una unidad: "treinta y uno" es una
    /// cifra, "diez y quince" son dos. Devuelve el inicio y la longitud de cada parte dentro de la secuencia.
    /// </summary>
    private static List<(int Start, int Length)> Segments(string words)
    {
        var tokens = WordTokenPattern().Matches(words).ToList();
        var segments = new List<(int Start, int Length)>();
        var start = tokens[0].Index;
        for (var i = 1; i < tokens.Count - 1; i++)
        {
            if (!tokens[i].Value.Equals("y", StringComparison.OrdinalIgnoreCase) || JoinsTensAndUnits(tokens[i - 1].Value, tokens[i + 1].Value))
            {
                continue;
            }

            var previous = tokens[i - 1];
            segments.Add((start, previous.Index + previous.Length - start));
            start = tokens[i + 1].Index;
        }

        var last = tokens[^1];
        segments.Add((start, last.Index + last.Length - start));
        return segments;
    }

    private static bool JoinsTensAndUnits(string tens, string units) =>
        NumberWords.GetValueOrDefault(tens) is >= 30 and <= 90 && NumberWords.GetValueOrDefault(units) is >= 1 and <= 9;

    /// <summary>"un mes", "una semana": el artículo vale 1 cuando va delante de una unidad de tiempo.</summary>
    private static bool IsArticleBeforeTimeUnit(string words, string text, int end) =>
        NumberWords.GetValueOrDefault(words) == 1
            && words.Length <= 3
            && TimeUnitAfterPattern().IsMatch(text, end);

    private static string? ParseDay(string day) =>
        int.TryParse(day, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? day
            : ParseWords(day)?.ToString(CultureInfo.InvariantCulture);

    private static DateOnly? TryDate(string year, string month, string day) =>
        DateOnly.TryParseExact($"{year}-{month.PadLeft(2, '0')}-{day.PadLeft(2, '0')}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>Marca las posiciones ocupadas: comprobar un solape cuesta lo que mide la coincidencia, no el número de cifras.</summary>
    private static bool Overlaps(bool[] taken, int start, int length) => Array.IndexOf(taken, true, start, length) >= 0;

    private static void Add(List<NumberMention> mentions, bool[] taken, NumberMention mention)
    {
        Array.Fill(taken, true, mention.Start, mention.Length);
        mentions.Add(mention);
    }

    private static string FullYear(string year) =>
        year.Length == 2 ? (TwoDigitYearBase + int.Parse(year, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture) : year;

    /// <summary>"1.500,75" → 1500.75: el punto (o un espacio) separa los miles y la coma, los decimales.</summary>
    private static decimal? ParseDigits(string digits) =>
        decimal.TryParse(
            ThousandsSeparatorPattern().Replace(digits, string.Empty).Replace(',', '.'),
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

    /// <summary>El "ciento" de "tanto por ciento" es la unidad del porcentaje, no el número 100. "Por cien euros" sí es 100.</summary>
    private static bool IsPercentageHundred(string text, Group number) =>
        number.Value.StartsWith("ciento", StringComparison.OrdinalIgnoreCase)
            && number.Index >= ForPrefix.Length
            && text.AsSpan(number.Index - ForPrefix.Length, ForPrefix.Length).Equals(ForPrefix, StringComparison.OrdinalIgnoreCase);

    private const string Unit = @"(?:\s*(?:%|€)|\s+(?:euros?|por\s+cien(?:to)?|por\s+100)(?![\p{L}\p{N}]))?";

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
        @"(?<![\p{N}.,])(?<number>\d{1,3}(?:[. \u00A0]\d{3})+(?:,\d+)?|\d+(?:,\d+)?)(?![\p{N}])(?:\s+(?<scale>mil|millones|millón)(?![\p{L}\p{N}]))?" + Unit,
        GuidelineRegex.Options,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex DigitsPattern();

    [GeneratedRegex(
        @"(?<![\p{N}/.-])(?<day>\d{1,2})(?<sep>[/.-])(?<month>\d{1,2})\k<sep>(?<year>\d{4}|\d{2})(?![\p{N}])",
        GuidelineRegex.Options,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?<day>\d{1,2}|\p{L}+)\s+de\s+" + MonthName + @"(?![\p{L}])(?:\s+del?\s+(?<year>\d{4})(?![\p{N}]))?",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex TextualDatePattern();

    [GeneratedRegex(@"\S+", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex WordTokenPattern();

    [GeneratedRegex(@"[. \u00A0](?=\d{3})", GuidelineRegex.Options, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex ThousandsSeparatorPattern();

    [GeneratedRegex(
        @"\G\s+(?:mes|año|día|semana|hora|vez|trimestre|semestre)(?![\p{L}])",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex TimeUnitAfterPattern();
}
