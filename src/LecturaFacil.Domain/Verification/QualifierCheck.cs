// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Verification;

/// <summary>
/// Lo que acompaña a una cifra también es parte del dato: "15 días hábiles" no es "15 días naturales", "250 € al mes" no
/// es "250 € al año" y "máximo de 250 €" no es "al menos 250 €". Cada grupo de palabras que aparece en el fragmento
/// original debe tener algún equivalente en el fragmento adaptado; si no, el dato se revisa.
/// </summary>
public static partial class QualifierCheck
{
    private static readonly Regex[] Groups =
    [
        BusinessDays(), CalendarDays(), Days(), Weeks(), Months(), Years(), Hours(), Maximum(), Minimum(), Percent(), Euros(),
    ];

    /// <summary>Si el fragmento adaptado conserva todos los calificativos del original.</summary>
    /// <exception cref="RegexMatchTimeoutException">Si un texto anómalo agota el tiempo de búsqueda (ReDoS).</exception>
    public static bool Keeps(string originalFragment, string adaptedFragment)
    {
        ArgumentNullException.ThrowIfNull(originalFragment);
        ArgumentNullException.ThrowIfNull(adaptedFragment);
        return Array.TrueForAll(Groups, group => !group.IsMatch(originalFragment) || group.IsMatch(adaptedFragment));
    }

    private const string End = @"(?![\p{L}\p{N}])";
    private const string Start = @"(?<![\p{L}\p{N}])";

    /// <summary>"No cuentan sábados, domingos ni festivos" explica los días hábiles.</summary>
    [GeneratedRegex(Start + "(?:hábil(?:es)?|habil(?:es)?|festivos?|sábados?|sabados?)" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex BusinessDays();

    [GeneratedRegex(Start + "(?:naturales?|todos los días)" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex CalendarDays();

    [GeneratedRegex(Start + @"(?:días?|dias?|diari\p{L}*)" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Days();

    [GeneratedRegex(Start + @"(?:semanas?|semanal\p{L}*)" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Weeks();

    [GeneratedRegex(Start + @"(?:mes(?:es)?|mensual\p{L}*)" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Months();

    [GeneratedRegex(Start + @"(?:años?|anual\p{L}*)" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Years();

    [GeneratedRegex(Start + "(?:horas?)" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Hours();

    [GeneratedRegex(
        Start + @"(?:máximo|maximo|como mucho|no más de|no mas de|menos de|no excedan?|no exceda|no superen?|tope|límite|limite)" + End,
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Maximum();

    [GeneratedRegex(
        Start + @"(?:mínimo|minimo|al menos|como poco|más de|mas de|superior(?:es)? a)" + End,
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Minimum();

    [GeneratedRegex(@"%|" + Start + @"(?:por ciento|por cien|por 100)" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Percent();

    [GeneratedRegex(@"€|" + Start + "euros?" + End, GuidelineRegex.IgnoreCase, GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex Euros();
}
