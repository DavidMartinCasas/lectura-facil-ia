// SPDX-License-Identifier: Apache-2.0
using System.Text.RegularExpressions;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>
/// Voz pasiva: "ser/estar + participio" ("será revisada", "sido aprobada"), pasiva refleja con "se" ("se publicará",
/// "se publican") y con el pronombre al final tras un verbo de obligación o posibilidad ("deberán presentarse").
/// </summary>
public sealed partial class PassiveVoiceRule : IGuidelineRule
{
    private const string Auxiliary =
        "es|son|era|eran|fue|fueron|será|serán|sería|serían|sea|sean|fuera|fueran|fuese|fuesen|ser|sido|siendo|"
        + "está|están|estaba|estaban|estará|estarán|estuvo|estuvieron";

    private const string Participle =
        @"(?:\p{L}{2,}[ai]d[oa]s?|(?:dispuest|puest|hech|escrit|abiert|cubiert|resuelt|vuelt|vist|dich|impres|previst|suscrit|inscrit)[oa]s?)";

    // Presente en singular: solo verbos típicos de los textos administrativos, porque "se + verbo" es muy ambiguo.
    private const string AdministrativeSingular = "concede|publica|abona|requiere|admite|otorga|convoca|exige|establece|aprueba";

    // Verbos pronominales en plural que no son pasiva ("las oficinas se encuentran en…").
    private const string Pronominal = "encuentran|refieren|dirigen|trasladan|quedan|llaman|sienten|van|ven";

    private const string Modal = "debe|deben|deberá|deberán|podrá|podrán|puede|pueden|habrá|habrán|ha|han";

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.PassiveVoice);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (Match match in PassivePattern().Matches(text.Value))
        {
            yield return Finding.At(Guideline, text.Value, match.Index, match.Index + match.Length,
                "Usa la voz activa: di quién hace la acción (por ejemplo, «El Ayuntamiento publicará la lista»).");
        }
    }

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])(?:(?:" + Auxiliary + @")\s+(?:\p{L}+mente\s+)?" + Participle
            + @"|se\s+(?:" + AdministrativeSingular + @"|(?!" + Pronominal + @"(?![\p{L}]))\p{L}{2,}(?:ará|erá|irá|arán|erán|irán|an|en))"
            + @"|(?:" + Modal + @")\s+(?:de\s+)?\p{L}{2,}(?:arse|erse|irse))(?![\p{L}\p{N}])",
        GuidelineRegex.IgnoreCase,
        GuidelineRegex.TimeoutMilliseconds)]
    private static partial Regex PassivePattern();
}
