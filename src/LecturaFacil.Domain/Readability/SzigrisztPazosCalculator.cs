// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Readability;

/// <summary>Índice de perspicuidad de Szigriszt-Pazos: <c>IFSZ = 206,835 − 62,3 · (sílabas / palabras) − (palabras / frases)</c>.</summary>
public static class SzigrisztPazosCalculator
{
    private const double BaseScore = 206.835;
    private const double SyllablesPerWordWeight = 62.3;

    /// <summary>Calcula el índice de un texto. El texto debe tener al menos una palabra.</summary>
    public static ReadabilityScore Calculate(string text) => Calculate(TextStatistics.From(text));

    public static ReadabilityScore Calculate(TextStatistics statistics)
    {
        if (!statistics.HasWords)
        {
            throw new ArgumentException("The text has no words to measure.", nameof(statistics));
        }

        var syllablesPerWord = statistics.Syllables / (double)statistics.Words;
        var wordsPerSentence = statistics.Words / (double)statistics.Sentences;
        return ReadabilityScore.FromValue(BaseScore - (SyllablesPerWordWeight * syllablesPerWord) - wordsPerSentence);
    }
}
