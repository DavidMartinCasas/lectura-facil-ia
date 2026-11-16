// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Tests.Guidelines;

public sealed class SentenceSegmenterTests
{
    [Fact]
    public void Split_WhenTextHasTwoSentences_ReturnsBothWithTheirPositions()
    {
        const string text = "El Ayuntamiento da ayudas. Puede pedirlas en la web.";

        var sentences = SentenceSegmenter.Split(text);

        sentences.Select(sentence => sentence.Text).ShouldBe(["El Ayuntamiento da ayudas.", "Puede pedirlas en la web."]);
        sentences[1].Start.ShouldBe(27);
        sentences[1].End.ShouldBe(text.Length);
        text[sentences[1].Start..sentences[1].End].ShouldBe(sentences[1].Text);
    }

    [Theory]
    [InlineData("Lo firma el Sr. García. Después se publica.")]
    [InlineData("Según el art. 5 de la ley. Después se publica.")]
    [InlineData("Consulta el núm. 7 del boletín. Después se publica.")]
    [InlineData("La Sra. Pérez y el Dr. López lo firman. Después se publica.")]
    [InlineData("Lo firma J. García en nombre de todos. Después se publica.")]
    public void Split_WhenTextHasAbbreviations_DoesNotCutAfterThem(string text)
    {
        var sentences = SentenceSegmenter.Split(text);

        sentences.Count.ShouldBe(2);
        sentences[1].Text.ShouldBe("Después se publica.");
    }

    [Theory]
    [InlineData("Lea el capítulo V. Después rellene la solicitud.")]
    [InlineData("Anexo I. Solicitud de la ayuda.")]
    public void Split_WhenSingleLetterIsANumbering_Cuts(string text)
    {
        SentenceSegmenter.Split(text).Count.ShouldBe(2);
    }

    [Fact]
    public void Split_WhenNextWordIsLowercase_DoesNotCut()
    {
        var sentences = SentenceSegmenter.Split("Trae el DNI, la factura, etc. y el contrato.");

        sentences.Count.ShouldBe(1);
    }

    [Fact]
    public void Split_WhenTextHasLineBreaks_EachLineIsASentence()
    {
        const string text = "Requisitos\n\n- Vivir de alquiler\n- No tener otra casa";

        var sentences = SentenceSegmenter.Split(text);

        sentences.Select(sentence => sentence.Text).ShouldBe(["Requisitos", "- Vivir de alquiler", "- No tener otra casa"]);
    }

    [Theory]
    [InlineData("¿Quién puede pedirla? Las personas que viven de alquiler.")]
    [InlineData("¡Atención! Las personas que viven de alquiler.")]
    [InlineData("Se acabó el plazo… Las personas que viven de alquiler.")]
    public void Split_WhenSentenceEndsWithOtherPunctuation_Cuts(string text)
    {
        SentenceSegmenter.Split(text).Count.ShouldBe(2);
    }

    [Fact]
    public void Split_WhenNumberHasDecimals_DoesNotCut()
    {
        SentenceSegmenter.Split("Gana menos de 2.5 veces el salario. Fin del texto.").Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    [InlineData("...")]
    public void Split_WhenTextHasNoWords_ReturnsNoSentences(string text)
    {
        SentenceSegmenter.Split(text).ShouldBeEmpty();
    }

    [Fact]
    public void Split_WhenTextDoesNotEndWithPunctuation_KeepsTheLastSentence()
    {
        var sentences = SentenceSegmenter.Split("Primera frase. Segunda sin punto");

        sentences.Select(sentence => sentence.Text).ShouldBe(["Primera frase.", "Segunda sin punto"]);
    }
}
