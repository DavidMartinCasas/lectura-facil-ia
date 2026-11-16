// SPDX-License-Identifier: Apache-2.0
using System.Globalization;

namespace LecturaFacil.Domain.Readability;

/// <summary>Frases, palabras y sílabas de un texto, en una sola pasada (coste lineal con su tamaño).</summary>
/// <remarks>
/// <para>Palabra: secuencia de letras. Los números no cuentan como palabras y un guion separa dos palabras.</para>
/// <para>Fin de frase: punto, interrogación, exclamación, puntos suspensivos o salto de línea (los títulos y las listas de lectura
/// fácil suelen ir sin punto). Un punto entre dos cifras ("3.5") no cierra la frase. Solo cuentan las frases con alguna palabra.</para>
/// <para>Limitación: las abreviaturas con punto ("Sr.", "art.") cierran frase y hacen el texto algo más fácil de lo que es.</para>
/// </remarks>
public readonly record struct TextStatistics(int Sentences, int Words, int Syllables)
{
    public bool HasWords => Words > 0;

    public static TextStatistics From(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var normalized = SpanishSyllableCounter.ToNfc(text).AsSpan();
        var sentences = 0;
        var words = 0;
        var syllables = 0;
        var sentenceHasWords = false;
        var index = 0;

        while (index < normalized.Length)
        {
            if (char.IsLetter(normalized[index]))
            {
                var length = WordLength(normalized[index..]);
                syllables += SpanishSyllableCounter.Count(normalized.Slice(index, length));
                words++;
                sentenceHasWords = true;
                index += length;
                continue;
            }

            if (sentenceHasWords && EndsSentence(normalized, index))
            {
                sentences++;
                sentenceHasWords = false;
            }

            index++;
        }

        if (sentenceHasWords)
        {
            sentences++;
        }

        return new TextStatistics(sentences, words, syllables);
    }

    private static int WordLength(ReadOnlySpan<char> text)
    {
        var length = 0;
        while (length < text.Length && (char.IsLetter(text[length]) || IsCombiningMark(text[length])))
        {
            length++;
        }

        return length;
    }

    private static bool IsCombiningMark(char character) =>
        char.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark;

    private static bool EndsSentence(ReadOnlySpan<char> text, int index) => text[index] switch
    {
        '?' or '!' or '…' or '\n' or '\r' => true,
        '.' => !IsDecimalPoint(text, index),
        _ => false,
    };

    private static bool IsDecimalPoint(ReadOnlySpan<char> text, int index) =>
        index > 0 && index + 1 < text.Length && char.IsDigit(text[index - 1]) && char.IsDigit(text[index + 1]);
}
