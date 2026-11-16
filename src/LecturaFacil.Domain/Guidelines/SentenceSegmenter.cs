// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>
/// Separa un texto en castellano en frases. Corta tras <c>. ! ? …</c> seguidos de espacio y de algo que no empieza
/// en minúscula, y en cada salto de línea (títulos y elementos de lista son frases). No corta tras abreviaturas
/// comunes ("Sr.", "art.", "núm.") ni tras iniciales ("J. García"). Recorre el texto una sola vez.
/// </summary>
public static class SentenceSegmenter
{
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "sr", "sra", "sres", "sras", "srta", "d", "dña", "dª", "dr", "dra", "dres", "ud", "uds", "vd", "vds",
        "excmo", "excma", "ilmo", "ilma", "art", "arts", "núm", "núms", "nº", "n.º", "pág", "págs", "p", "pp",
        "ej", "apdo", "avda", "av", "c", "tel", "telf", "aprox", "vol", "cap", "cf", "vid", "admón",
        "s.a", "s.l", "ss", "fdo", "dto", "dpto", "prov", "ref", "máx", "mín", "aptdo", "op", "cit", "ntra", "ntro",
    };

    private static readonly HashSet<string> NumberingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "capítulo", "anexo", "título", "apartado", "letra", "sección", "parte", "tomo",
    };

    public static IReadOnlyList<Sentence> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var sentences = new List<Sentence>();
        var start = 0;
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '\n')
            {
                AddIfHasWords(sentences, text, start, index);
                start = ++index;
                continue;
            }

            if (!IsTerminator(text[index]))
            {
                index++;
                continue;
            }

            var end = SkipClosingPunctuation(text, index);
            if (EndsSentence(text, index, end))
            {
                AddIfHasWords(sentences, text, start, end);
                start = end;
            }

            index = end;
        }

        AddIfHasWords(sentences, text, start, text.Length);
        return sentences;
    }

    private static bool IsTerminator(char character) => character is '.' or '!' or '?' or '…';

    private static int SkipClosingPunctuation(string text, int index)
    {
        var end = index;
        while (end < text.Length && (IsTerminator(text[end]) || text[end] is '"' or '\'' or '»' or '”' or '’' or ')'))
        {
            end++;
        }

        return end;
    }

    private static bool EndsSentence(string text, int terminator, int end)
    {
        if (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            return false;
        }

        if (text[terminator] == '.' && IsAbbreviation(text, terminator))
        {
            return false;
        }

        var next = end;
        while (next < text.Length && text[next] is ' ' or ' ')
        {
            next++;
        }

        return next >= text.Length || !char.IsLower(text[next]);
    }

    /// <summary>La palabra justo antes del punto es una abreviatura conocida o una inicial ("J.").</summary>
    private static bool IsAbbreviation(string text, int period)
    {
        var wordStart = period;
        while (wordStart > 0 && !char.IsWhiteSpace(text[wordStart - 1]) && text[wordStart - 1] != '(')
        {
            wordStart--;
        }

        var word = text[wordStart..period].TrimStart('"', '\'', '«', '“', '¿', '¡');
        if (word.Length == 1 && char.IsLetter(word[0]))
        {
            // "capítulo V." o "anexo I." terminan frase; "J. García" no.
            return !NumberingWords.Contains(PreviousWord(text, wordStart));
        }

        return Abbreviations.Contains(word);
    }

    private static string PreviousWord(string text, int wordStart)
    {
        var end = wordStart;
        while (end > 0 && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var start = end;
        while (start > 0 && char.IsLetter(text[start - 1]))
        {
            start--;
        }

        return text[start..end];
    }

    private static void AddIfHasWords(List<Sentence> sentences, string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        var span = text.AsSpan(start, end - start);
        if (HasLetterOrDigit(span))
        {
            sentences.Add(new Sentence(start, end, span.ToString()));
        }
    }

    private static bool HasLetterOrDigit(ReadOnlySpan<char> span)
    {
        foreach (var character in span)
        {
            if (char.IsLetterOrDigit(character))
            {
                return true;
            }
        }

        return false;
    }
}
