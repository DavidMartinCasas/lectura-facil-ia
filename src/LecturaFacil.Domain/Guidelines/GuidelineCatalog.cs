// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Catálogo de las pautas que se detectan. Se siembra en la base de datos.</summary>
public static class GuidelineCatalog
{
    public static readonly IReadOnlyList<Guideline> All =
    [
        new(GuidelineCodes.ShortSentences, "Frases cortas", Severity.Medium),
        new(GuidelineCodes.OneIdeaPerSentence, "Una idea por frase", Severity.Low),
        new(GuidelineCodes.Acronyms, "Sin siglas sin explicar", Severity.High),
        new(GuidelineCodes.Percentages, "Sin porcentajes", Severity.Medium),
        new(GuidelineCodes.NumbersInDigits, "Números escritos con cifras", Severity.Low),
        new(GuidelineCodes.PassiveVoice, "Sin voz pasiva", Severity.Medium),
        new(GuidelineCodes.DoubleNegation, "Sin negaciones dobles", Severity.High),
        new(GuidelineCodes.RomanNumerals, "Sin números romanos", Severity.Medium),
        new(GuidelineCodes.DifficultWords, "Palabras difíciles explicadas", Severity.Low),
        new(GuidelineCodes.LegalJargon, "Sin expresiones jurídicas complejas", Severity.High),
        new(GuidelineCodes.NumericDate, "Fechas completas", Severity.Low),
    ];

    private static readonly Dictionary<string, Guideline> ByCode = All.ToDictionary(guideline => guideline.Code, StringComparer.Ordinal);

    public static Guideline Find(string code) =>
        ByCode.TryGetValue(code, out var guideline)
            ? guideline
            : throw new KeyNotFoundException($"Unknown guideline code: {code}.");
}
