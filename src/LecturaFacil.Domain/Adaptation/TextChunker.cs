// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Adaptation;

/// <summary>
/// Divide un texto largo en partes de como mucho <c>maxCharacters</c> para adaptarlas por separado. Junta párrafos
/// enteros mientras quepan; un párrafo que no cabe se divide por frases y una frase que no cabe, entre palabras.
/// Solo corta una palabra si ella sola supera el límite, y nunca deja medio par sustituto.
/// </summary>
public static class TextChunker
{
    private const string ParagraphSeparator = "\n\n";
    private const string WordSeparator = " ";

    public static IReadOnlyList<string> Split(string text, int maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);

        var paragraphs = text.Split(ParagraphSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pieces = paragraphs.SelectMany(paragraph => SplitParagraph(paragraph, maxCharacters));
        return Pack(pieces, maxCharacters);
    }

    /// <summary>Partes de un párrafo, cada una con el separador que la une a la anterior.</summary>
    private static IEnumerable<Piece> SplitParagraph(string paragraph, int maxCharacters)
    {
        if (paragraph.Length <= maxCharacters)
        {
            yield return new Piece(paragraph, ParagraphSeparator);
            yield break;
        }

        var separator = ParagraphSeparator;
        foreach (var sentence in SentenceSegmenter.Split(paragraph))
        {
            foreach (var part in SplitSentence(sentence.Text, maxCharacters))
            {
                yield return new Piece(part, separator);
                separator = WordSeparator;
            }
        }
    }

    private static IEnumerable<string> SplitSentence(string sentence, int maxCharacters)
    {
        if (sentence.Length <= maxCharacters)
        {
            return [sentence];
        }

        var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(word => SplitWord(word, maxCharacters));
        return Pack(words.Select(word => new Piece(word, WordSeparator)), maxCharacters);
    }

    private static IEnumerable<string> SplitWord(string word, int maxCharacters)
    {
        var start = 0;
        while (word.Length - start > maxCharacters)
        {
            var length = char.IsHighSurrogate(word[start + maxCharacters - 1]) ? maxCharacters - 1 : maxCharacters;
            if (length == 0)
            {
                // Un límite de un carácter con un par sustituto: el par va entero.
                length = 2;
            }

            yield return word.Substring(start, length);
            start += length;
        }

        yield return word[start..];
    }

    /// <summary>Junta piezas consecutivas mientras quepan en el límite.</summary>
    private static List<string> Pack(IEnumerable<Piece> pieces, int maxCharacters)
    {
        var chunks = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var piece in pieces)
        {
            if (current.Length > 0 && current.Length + piece.Separator.Length + piece.Text.Length > maxCharacters)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(piece.Separator);
            }

            current.Append(piece.Text);
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    private readonly record struct Piece(string Text, string Separator);
}
