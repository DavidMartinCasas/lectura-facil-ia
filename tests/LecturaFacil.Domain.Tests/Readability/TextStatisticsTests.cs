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
    public void From_WhenTextHasNumbers_DoesNotCountThemAsWordsNorTheirDecimalPointAsASentenceEnd()
    {
        var statistics = TextStatistics.From("Pague 3.5 euros... antes del 10/12/2026.");

        statistics.ShouldBe(new TextStatistics(Sentences: 2, Words: 4, Syllables: 7));
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
    public void From_WhenTextIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => TextStatistics.From(null!));
    }
}
