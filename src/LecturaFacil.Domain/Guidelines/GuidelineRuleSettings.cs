// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Umbrales y listas configurables de las reglas.</summary>
public sealed record GuidelineRuleSettings(
    int MaxWordsPerSentence,
    int MaxConnectorsPerSentence,
    IReadOnlyCollection<string> AllowedAcronyms,
    IReadOnlyCollection<TermSuggestion> LegalJargon,
    IReadOnlyCollection<TermSuggestion> DifficultWords);
