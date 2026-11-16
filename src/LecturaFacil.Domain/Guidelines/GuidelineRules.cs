// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Guidelines;

/// <summary>Las reglas de todas las pautas del <see cref="GuidelineCatalog"/>. Una pauta nueva se añade aquí.</summary>
public static class GuidelineRules
{
    public static IReadOnlyList<IGuidelineRule> Create(GuidelineRuleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return
        [
            new ShortSentencesRule(settings.MaxWordsPerSentence),
            new OneIdeaPerSentenceRule(settings.MaxConnectorsPerSentence),
            new AcronymsRule(settings.AllowedAcronyms),
            new PercentagesRule(),
            new NumbersInDigitsRule(),
            new PassiveVoiceRule(),
            new DoubleNegationRule(),
            new RomanNumeralsRule(),
            new DifficultWordsRule(settings.DifficultWords),
            new LegalJargonRule(settings.LegalJargon),
            new NumericDateRule(),
        ];
    }
}
