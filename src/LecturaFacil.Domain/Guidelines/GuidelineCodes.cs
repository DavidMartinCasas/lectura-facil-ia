// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <summary>Códigos de las pautas de lectura fácil. Son estables: se guardan en la base de datos y los usa la interfaz.</summary>
public static class GuidelineCodes
{
    public const string ShortSentences = "FRASES_CORTAS";
    public const string OneIdeaPerSentence = "UNA_IDEA_POR_FRASE";
    public const string Acronyms = "SIN_SIGLAS";
    public const string Percentages = "SIN_PORCENTAJES";
    public const string NumbersInDigits = "NUMEROS_EN_CIFRAS";
    public const string PassiveVoice = "SIN_VOZ_PASIVA";
    public const string DoubleNegation = "SIN_NEGACIONES_DOBLES";
    public const string RomanNumerals = "SIN_NUMEROS_ROMANOS";
    public const string DifficultWords = "PALABRAS_DIFICILES";
    public const string LegalJargon = "SIN_EXPRESIONES_JURIDICAS";
    public const string NumericDate = "FECHAS_COMPLETAS";
    public const string ClearHeadings = "TITULOS_CLAROS";
}
