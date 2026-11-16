// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>
/// Palabras difíciles de una lista inicial, pequeña y configurable, con una alternativa más frecuente. El glosario y el
/// vocabulario frecuente completo llegarán con la adaptación.
/// </summary>
public sealed class DifficultWordsRule : IGuidelineRule
{
    private readonly TermList _words;

    public DifficultWordsRule(IEnumerable<TermSuggestion> words) => _words = new TermList(words);

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.DifficultWords);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return _words.Find(text.Value).Select(found => Finding.At(
            Guideline,
            text.Value,
            found.Match.Index,
            found.Match.Index + found.Match.Length,
            found.Alternative.Length > 0
                ? $"«{found.Match.Value}» es una palabra difícil. Usa «{found.Alternative}» o explícala."
                : $"«{found.Match.Value}» es una palabra difícil. Explícala con palabras sencillas."));
    }
}
