// SPDX-License-Identifier: Apache-2.0
using System.Globalization;

namespace LecturaFacil.Domain.Guidelines.Rules;

/// <summary>Frases con más palabras que el máximo configurado.</summary>
public sealed class ShortSentencesRule : IGuidelineRule
{
    private readonly int _maxWords;

    public ShortSentencesRule(int maxWords)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWords);
        _maxWords = maxWords;
    }

    public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.ShortSentences);

    public IEnumerable<Finding> Check(GuidelineText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var sentence in text.Sentences)
        {
            var words = Words.Count(sentence.Text);
            if (words > _maxWords)
            {
                yield return Finding.At(Guideline, text.Value, sentence.Start, sentence.End, string.Create(
                    CultureInfo.InvariantCulture,
                    $"Esta frase tiene {words} palabras. Divide la frase en frases más cortas, de {_maxWords} palabras como máximo."));
            }
        }
    }
}
