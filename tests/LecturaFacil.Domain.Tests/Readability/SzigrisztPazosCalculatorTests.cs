// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Domain.Tests.Readability;

public sealed class SzigrisztPazosCalculatorTests
{
    private const double Tolerance = 1;

    [Fact]
    public void Calculate_WhenStatisticsAreKnown_AppliesTheFormula()
    {
        // 206,835 − 62,3 · (200 / 100) − (100 / 5) = 206,835 − 124,6 − 20 = 62,235
        var score = SzigrisztPazosCalculator.Calculate(new TextStatistics(Sentences: 5, Words: 100, Syllables: 200));

        score.Value.ShouldBe(62.235, 0.0001);
        score.Grade.ShouldBe(ReadabilityGrade.Normal);
    }

    [Fact]
    public void Calculate_WhenTextIsShortAndSimple_ReturnsAVeryEasyScore()
    {
        // 2 frases, 7 palabras y 11 sílabas: 206,835 − 62,3 · 11/7 − 7/2 = 105,435
        var score = SzigrisztPazosCalculator.Calculate("El perro come. La casa es grande.");

        score.Value.ShouldBe(105.435, Tolerance);
        score.Grade.ShouldBe(ReadabilityGrade.VeryEasy);
    }

    [Fact]
    public void Calculate_WhenTextIsAdministrative_ReturnsAVeryDifficultScore()
    {
        // 1 frase, 12 palabras y 36 sílabas: 206,835 − 62,3 · 36/12 − 12/1 = 7,935
        const string text = "La persona solicitante deberá presentar la documentación acreditativa en el plazo establecido.";

        var score = SzigrisztPazosCalculator.Calculate(text);

        score.Value.ShouldBe(7.935, Tolerance);
        score.Grade.ShouldBe(ReadabilityGrade.VeryDifficult);
    }

    [Fact]
    public void Calculate_WhenTextIsAnEasyReadingAdaptation_ScoresHigherThanTheOriginal()
    {
        const string original = "La persona solicitante deberá presentar la documentación acreditativa en el plazo establecido.";
        const string adapted = "Tienes que dar los papeles. Hazlo antes de la fecha.";

        SzigrisztPazosCalculator.Calculate(adapted).Value
            .ShouldBeGreaterThan(SzigrisztPazosCalculator.Calculate(original).Value);
    }

    [Fact]
    public void Calculate_WhenThereAreNoWords_Throws()
    {
        Should.Throw<ArgumentException>(() => SzigrisztPazosCalculator.Calculate(new TextStatistics(0, 0, 0)));
    }
}
