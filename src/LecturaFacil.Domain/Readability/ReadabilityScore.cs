// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Readability;

/// <summary>
/// Índice de legibilidad Szigriszt-Pazos y su grado en la escala INFLESZ. Es un valor <b>aproximado</b>: depende del
/// contador de sílabas y de cómo se separan las frases (ver <see cref="SpanishSyllableCounter"/> y <see cref="TextStatistics"/>).
/// </summary>
/// <remarks>
/// Escala INFLESZ: &lt; 40 muy difícil; 40–55 algo difícil; 55–65 normal; 65–80 bastante fácil; &gt; 80 muy fácil.
/// En los límites, el valor exacto pertenece al tramo superior (40, 55 y 65), salvo 80, que sigue siendo "bastante fácil"
/// porque la escala dice "más de 80" para "muy fácil".
/// </remarks>
public sealed record ReadabilityScore
{
    public const double VeryDifficultUpperBound = 40;
    public const double SomewhatDifficultUpperBound = 55;
    public const double NormalUpperBound = 65;
    public const double QuiteEasyUpperBound = 80;

    private ReadabilityScore(double value, ReadabilityGrade grade)
    {
        Value = value;
        Grade = grade;
    }

    /// <summary>Valor del índice. Puede salir de 0–100 en textos muy cortos o muy largos.</summary>
    public double Value { get; }

    public ReadabilityGrade Grade { get; }

    /// <summary>Nombre del grado para mostrarlo al usuario.</summary>
    public string GradeLabel => LabelOf(Grade);

    public static ReadabilityScore FromValue(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The readability score must be a finite number.");
        }

        return new ReadabilityScore(value, Classify(value));
    }

    public static string LabelOf(ReadabilityGrade grade) => grade switch
    {
        ReadabilityGrade.VeryDifficult => "Muy difícil",
        ReadabilityGrade.SomewhatDifficult => "Algo difícil",
        ReadabilityGrade.Normal => "Normal",
        ReadabilityGrade.QuiteEasy => "Bastante fácil",
        ReadabilityGrade.VeryEasy => "Muy fácil",
        _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "Unknown readability grade."),
    };

    private static ReadabilityGrade Classify(double value) => value switch
    {
        < VeryDifficultUpperBound => ReadabilityGrade.VeryDifficult,
        < SomewhatDifficultUpperBound => ReadabilityGrade.SomewhatDifficult,
        < NormalUpperBound => ReadabilityGrade.Normal,
        <= QuiteEasyUpperBound => ReadabilityGrade.QuiteEasy,
        _ => ReadabilityGrade.VeryEasy,
    };
}
