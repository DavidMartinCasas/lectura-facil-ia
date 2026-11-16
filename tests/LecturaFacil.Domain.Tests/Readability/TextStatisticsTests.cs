// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Domain.Tests.Readability;

public sealed class TextStatisticsTests
{
    [Fact]
    public void From_WhenTextHasTwoSentences_CountsSentencesWordsAndSyllables()
    {
        var statistics = TextStatistics.From("El perro come. La casa es grande.");

        statistics.ShouldBe(new TextStatistics(Sentences: 2, Words: 7, Syllables: 11));
    }

    [Fact]
    public void From_WhenSentencesEndWithQuestionExclamationOrEllipsis_CountsEachOne()
    {
        var statistics = TextStatistics.From("¿Qué pasa? ¡Ven ya! Espera… Bien");

        statistics.Sentences.ShouldBe(4);
        statistics.Words.ShouldBe(6);
    }

    [Fact]
    public void From_WhenLinesHaveNoFinalPunctuation_EachLineIsASentence()
    {
        var statistics = TextStatistics.From("Requisitos\n- Tener 18 años\r\n- Vivir en el municipio");

        statistics.Sentences.ShouldBe(3);
        statistics.Words.ShouldBe(7);
    }

    [Fact]
    public void From_WhenTextHasNumbersAndAnEllipsisFollowedByLowercase_CountsOneSentenceWithoutNumbers()
    {
        var statistics = TextStatistics.From("Pague 3.5 euros... antes del 10/12/2026.");

        statistics.ShouldBe(new TextStatistics(Sentences: 1, Words: 4, Syllables: 7));
    }

    [Theory]
    [InlineData("Presente el D.N.I. en la sede.")]
    [InlineData("Consulte www.sede.gob.es para más información.")]
    [InlineData("Según el art. 3 de la orden, puede pedirla.")]
    [InlineData("Escriba a registro@ayto.es si tiene dudas.")]
    [InlineData("La ayuda es de 1.500,50 euros y se paga a las 10.30 horas.")]
    [InlineData("La persona solicitante deberá\npresentar la documentación.")]
    [InlineData("Puede pedirla, etc. y entregarla después.")]
    public void From_WhenAPointOrLineBreakDoesNotEndTheSentence_CountsOneSentence(string text)
    {
        TextStatistics.From(text).Sentences.ShouldBe(1);
    }

    [Fact]
    public void From_WhenTextHasOrdinalIndicators_DoesNotCountThemAsWords()
    {
        var statistics = TextStatistics.From("Requisito 1.º: tener 18 años. La 2.ª fase es en mayo.");

        statistics.Sentences.ShouldBe(2);
        statistics.Words.ShouldBe(8);
    }

    [Fact]
    public void From_WhenWordsAreHyphenated_CountsEachPart()
    {
        TextStatistics.From("Informe socio-económico.").Words.ShouldBe(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    [InlineData("123 . 456 !")]
    public void From_WhenTextHasNoWords_ReturnsZeroes(string text)
    {
        var statistics = TextStatistics.From(text);

        statistics.ShouldBe(new TextStatistics(0, 0, 0));
        statistics.HasWords.ShouldBeFalse();
    }

    [Fact]
    public void From_WhenTextHasALoneSurrogate_StillCountsTheWords()
    {
        TextStatistics.From("Hola \ud800 mundo.").ShouldBe(new TextStatistics(Sentences: 1, Words: 2, Syllables: 4));
    }

    [Fact]
    public void From_WhenTextIsOnlyCombiningMarks_HasNoWords()
    {
        TextStatistics.From("\u0301\u0301").HasWords.ShouldBeFalse();
    }

    [Fact]
    public void From_WhenTextIsHugeAndPathological_FinishesInLinearTime()
    {
        const int Size = 2_000_000;
        var oneHugeWord = new string('a', Size);
        var onlyPunctuation = new string('.', Size);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        TextStatistics.From(oneHugeWord).ShouldBe(new TextStatistics(Sentences: 1, Words: 1, Syllables: Size));
        TextStatistics.From(onlyPunctuation).HasWords.ShouldBeFalse();

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void From_WhenTextIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => TextStatistics.From(null!));
    }
}
