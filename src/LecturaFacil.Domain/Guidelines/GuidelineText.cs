// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Texto que se analiza, con sus frases ya separadas una sola vez para todas las reglas.</summary>
public sealed class GuidelineText
{
    private GuidelineText(string value, IReadOnlyList<Sentence> sentences)
    {
        Value = value;
        Sentences = sentences;
    }

    public string Value { get; }

    public IReadOnlyList<Sentence> Sentences { get; }

    public static GuidelineText Create(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new GuidelineText(text, SentenceSegmenter.Split(text));
    }
}
