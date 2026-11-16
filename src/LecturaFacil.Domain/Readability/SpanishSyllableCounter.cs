// SPDX-License-Identifier: Apache-2.0

namespace LecturaFacil.Domain.Readability;

/// <summary>
/// Cuenta las sílabas de una palabra en castellano con reglas, sin diccionario. Lo que cuenta son los núcleos
/// vocálicos: cada grupo de vocales seguidas es una sílaba salvo donde hay hiato.
/// </summary>
/// <remarks>
/// <para>Reglas implementadas:</para>
/// <list type="bullet">
/// <item>Vocales fuertes: a, e, o (con o sin tilde). Débiles: i, u, ü. Las débiles con tilde (í, ú) forman hiato con una fuerte.</item>
/// <item>Diptongo: débil + fuerte, fuerte + débil o dos débiles distintas (ui, iu, también con tilde: "construí").
///   Triptongo: débil + fuerte + débil ("buey", "averiguáis"). Ambos cuentan como una sílaba.</item>
/// <item>Hiato: dos fuertes ("leer", "aéreo"), fuerte + í/ú ("país", "día") o dos débiles iguales ("chiita").</item>
/// <item>La u de "que", "qui", "gue" y "gui" no suena y no cuenta. "guion" es monosílaba según la
///   Ortografía de la RAE (2010): g + [u muda] + io (diptongo). La ü sí cuenta ("pingüino").</item>
/// <item>La y es vocal cuando no le sigue otra vocal ("hoy", "rey", "y"); si no, es consonante ("ayer", "leyes").</item>
/// <item>La h separa vocales ("ahora", "búho"). Los grupos consonánticos (pr, bl, tr, ll, rr, ch…) no cambian el número de
///   núcleos, así que no hace falta tratarlos aparte.</item>
/// </list>
/// <para>Limitaciones conocidas (el índice es aproximado):</para>
/// <list type="bullet">
/// <item>La h intercalada puede formar diptongo en la pronunciación ("prohibir" se cuenta pro-hi-bir, 3).</item>
/// <item>Siglas y palabras sin vocales ("DNI", "pvp") cuentan como una sílaba.</item>
/// <item>Los hiatos sin tilde que dependen de la pronunciación ("guion" frente a "gui-ón" antes de 2010, "fluir") siguen la norma
///   ortográfica, no la pronunciación de cada hablante. Las letras de otros idiomas (à, ö…) se tratan como consonantes.</item>
/// </list>
/// </remarks>
public static class SpanishSyllableCounter
{
    /// <summary>Número de sílabas de <paramref name="word"/>. Cero si no tiene letras; al menos una si las tiene.</summary>
    public static int Count(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return Count(word.AsSpan());
    }

    /// <inheritdoc cref="Count(string)"/>
    public static int Count(ReadOnlySpan<char> word)
    {
        var hasLetters = false;
        var syllables = 0;
        var previous = '\0';
        var previousIsVowel = false;

        for (var index = 0; index < word.Length; index++)
        {
            var current = char.ToLowerInvariant(word[index]);
            if (!char.IsLetter(current))
            {
                continue;
            }

            hasLetters = true;
            var next = NextLetter(word, index);
            if (!IsVowel(current, previous, next))
            {
                previousIsVowel = false;
                previous = current;
                continue;
            }

            if (!previousIsVowel || IsHiatus(previous, current))
            {
                syllables++;
            }

            previousIsVowel = true;
            previous = current;
        }

        return hasLetters ? Math.Max(syllables, 1) : 0;
    }

    private static char NextLetter(ReadOnlySpan<char> word, int index) =>
        index + 1 < word.Length ? char.ToLowerInvariant(word[index + 1]) : '\0';

    private static bool IsVowel(char letter, char previous, char next) => letter switch
    {
        'a' or 'e' or 'o' or 'á' or 'é' or 'ó' or 'i' or 'í' or 'ú' or 'ü' => true,
        'u' => !IsSilentU(previous, next),
        'y' => !IsVowelLetter(next),
        _ => false,
    };

    // "que", "qui", "gue", "gui": la u solo marca el sonido de la consonante.
    private static bool IsSilentU(char previous, char next) =>
        previous == 'q' || (previous == 'g' && next is 'e' or 'i' or 'é' or 'í');

    private static bool IsVowelLetter(char letter) =>
        letter is 'a' or 'e' or 'i' or 'o' or 'u' or 'á' or 'é' or 'í' or 'ó' or 'ú' or 'ü';

    private static bool IsHiatus(char first, char second)
    {
        if (IsStrong(first) && IsStrong(second))
        {
            return true;
        }

        if ((IsStrong(first) && IsAccentedWeak(second)) || (IsAccentedWeak(first) && IsStrong(second)))
        {
            return true;
        }

        return IsWeak(first) && IsWeak(second) && BaseOf(first) == BaseOf(second);
    }

    private static bool IsStrong(char vowel) => vowel is 'a' or 'e' or 'o' or 'á' or 'é' or 'ó';

    private static bool IsAccentedWeak(char vowel) => vowel is 'í' or 'ú';

    private static bool IsWeak(char vowel) => !IsStrong(vowel);

    private static char BaseOf(char vowel) => vowel switch
    {
        'í' or 'y' => 'i',
        'ú' or 'ü' => 'u',
        _ => vowel,
    };
}
