// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>Expresiones jurídicas complejas de una lista configurable ("en virtud de", "sin perjuicio de"…).</summary>
public sealed class LegalJargonRule : IGuidelineRule
{
    private readonly TermList _expressions;

    public LegalJargonRule(IEnumerable<TermSuggestion> expressions) => _expressions = new TermList(expressions);

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.LegalJargon);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return _expressions.Find(text.Value).Select(found => Finding.At(
            Guideline,
            text.Value,
            found.Match.Index,
            found.Match.Index + found.Match.Length,
            found.Alternative.Length > 0
                ? $"Cambia «{found.Match.Value}» por una expresión sencilla, por ejemplo «{found.Alternative}»."
                : $"Cambia «{found.Match.Value}» por palabras sencillas que explican lo que quiere decir."));
    }
}
