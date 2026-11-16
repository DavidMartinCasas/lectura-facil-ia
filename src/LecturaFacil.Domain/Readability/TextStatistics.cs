// SPDX-License-Identifier: Apache-2.0
using System.Globalization;

namespace LecturaFacil.Domain.Readability;

/// <summary>Frases, palabras y sílabas de un texto, en una sola pasada (coste lineal con su tamaño).</summary>
/// <remarks>
/// <para>Palabra: secuencia de letras. Los números, "º" y "ª" no cuentan como palabras y un guion separa dos palabras.</para>
/// <para>Fin de frase: interrogación o exclamación; punto, puntos suspensivos o salto de línea salvo que el texto siga
/// en minúscula (líneas cortadas por el PDF, "etc. y") o el punto es de una abreviatura seguida de número ("art. 3"). Tampoco cierra frase un punto
/// pegado a lo que sigue ("3.5", "1.500", "1.º", "www.sede.es", "D.N.I"). Un salto de línea seguido de mayúscula o de
/// un guion de lista sí la cierra: los títulos y las listas de lectura fácil suelen ir sin punto. Solo cuentan las frases
/// con alguna palabra.</para>
/// <para>Limitaciones: una abreviatura seguida de mayúscula ("Sr. García") cierra frase; los números y las siglas
/// ("DNI" = 1 sílaba) hacen el texto algo más fácil de lo que es. El resultado es aproximado.</para>
/// </remarks>
public readonly record struct TextStatistics(int Sentences, int Words, int Syllables)
{
    // Abreviaturas que suelen ir delante de un número en textos administrativos.
    private static readonly HashSet<string> NumberAbbreviations =
        ["art", "arts", "núm", "núms", "n", "pág", "págs", "p", "apdo", "apdos", "cap", "vol", "disp"];

    private static readonly int MaxAbbreviationLength = NumberAbbreviations.Max(abbreviation => abbreviation.Length);

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
            if (IsWordLetter(normalized[index]))
            {
                var length = WordLength(normalized[index..]);
                syllables += SpanishSyllableCounter.Count(normalized.Slice(index, length));
                words++;
                sentenceHasWords = true;
                index += length;
                continue;
            }

            if (!sentenceHasWords || !IsTerminator(normalized[index]))
            {
                index++;
                continue;
            }

            // Cada espacio o punto se mira una sola vez: si la frase sigue, se salta hasta lo siguiente ("euros... antes").
            var next = SkipBlanksAndPoints(normalized, index + 1);
            if (ContinuesSentence(normalized, index, next))
            {
                index = Math.Max(next, index + 1);
                continue;
            }

            sentences++;
            sentenceHasWords = false;
            index++;
        }

        if (sentenceHasWords)
        {
            sentences++;
        }

        return new TextStatistics(sentences, words, syllables);
    }

    private static bool IsWordLetter(char character) => char.IsLetter(character) && character is not ('º' or 'ª');

    private static int WordLength(ReadOnlySpan<char> text)
    {
        var length = 0;
        while (length < text.Length && (IsWordLetter(text[length]) || IsCombiningMark(text[length])))
        {
            length++;
        }

        return length;
    }

    private static bool IsCombiningMark(char character) =>
        char.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark;

    private static bool IsTerminator(char character) => character is '.' or '?' or '!' or '…' or '\n' or '\r';

    private static int SkipBlanksAndPoints(ReadOnlySpan<char> text, int index)
    {
        while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] is '.' or '…'))
        {
            index++;
        }

        return index;
    }

    private static bool ContinuesSentence(ReadOnlySpan<char> text, int terminator, int next)
    {
        if (text[terminator] is '?' or '!' || next >= text.Length)
        {
            return false;
        }

        var glued = next == terminator + 1;
        if (glued && text[terminator] == '.' && IsGluedToPoint(text[next]))
        {
            return true;
        }

        if (char.IsDigit(text[next]))
        {
            // "mañana. 15 personas" son dos frases; "art. 3" es una.
            return text[terminator] == '.' && IsAfterAbbreviation(text, terminator);
        }

        return char.IsLower(text[next]);
    }

    private static bool IsGluedToPoint(char character) => char.IsLetterOrDigit(character);

    private static bool IsAfterAbbreviation(ReadOnlySpan<char> text, int point)
    {
        var start = point;
        while (start > 0 && point - start < MaxAbbreviationLength && IsWordLetter(text[start - 1]))
        {
            start--;
        }

        if (start > 0 && IsWordLetter(text[start - 1]))
        {
            return false;
        }

        var word = text[start..point].ToString().ToLowerInvariant();
        return NumberAbbreviations.Contains(word);
    }
}
