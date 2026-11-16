// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Verification;

public sealed class NumberNormalizerTests
{
    [Theory]
    [InlineData("cero", 0)]
    [InlineData("dos", 2)]
    [InlineData("quince", 15)]
    [InlineData("dieciséis", 16)]
    [InlineData("veinte", 20)]
    [InlineData("veintiuno", 21)]
    [InlineData("veintitrés", 23)]
    [InlineData("treinta", 30)]
    [InlineData("treinta y uno", 31)]
    [InlineData("cuarenta y cinco", 45)]
    [InlineData("noventa y nueve", 99)]
    [InlineData("cien", 100)]
    [InlineData("ciento uno", 101)]
    [InlineData("ciento veinte", 120)]
    [InlineData("doscientos cincuenta", 250)]
    [InlineData("quinientas", 500)]
    [InlineData("novecientos noventa y nueve", 999)]
    [InlineData("mil", 1000)]
    [InlineData("dos mil quinientos", 2500)]
    [InlineData("Quince", 15)]
    public void Find_WhenTheNumberIsWrittenInWords_ReturnsItsValue(string text, int expected) =>
        SingleNumber(text).ShouldBe(expected);

    [Theory]
    [InlineData("15", 15)]
    [InlineData("250", 250)]
    [InlineData("1500", 1500)]
    [InlineData("1.500", 1500)]
    [InlineData("1.000.000", 1000000)]
    [InlineData("2,5", 2.5)]
    [InlineData("2,50", 2.5)]
    [InlineData("1.500,75", 1500.75)]
    public void Find_WhenTheNumberIsWrittenInDigits_UsesTheSpanishSeparators(string text, decimal expected) =>
        SingleNumber(text).ShouldBe(expected);

    [Theory]
    [InlineData("un máximo de 250 €", 250)]
    [InlineData("250 euros al mes", 250)]
    [InlineData("250€", 250)]
    [InlineData("el 40 % de la renta", 40)]
    [InlineData("el 40% de la renta", 40)]
    [InlineData("el cuarenta por ciento de la renta", 40)]
    [InlineData("en el plazo de quince días hábiles", 15)]
    public void Find_IgnoresTheUnitAndKeepsTheValue(string text, decimal expected) =>
        SingleNumber(text).ShouldBe(expected);

    [Fact]
    public void Find_WhenAPercentageIsWrittenInWords_DoesNotReadPorCientoAsAHundred() =>
        NumberNormalizer.Values("el cuarenta por ciento").ShouldBe([NumericValue.Of(40)]);

    [Fact]
    public void Find_KeepsTheUnitInTheMentionText()
    {
        var mention = NumberNormalizer.Find("Con un máximo de 250 € al mes.").ShouldHaveSingleItem();

        mention.Text.ShouldBe("250 €");
    }

    [Fact]
    public void Find_GivesThePositionOfEachMentionInTheText()
    {
        const string text = "Tiene quince días y 250 euros.";

        var mentions = NumberNormalizer.Find(text);

        mentions.Select(mention => text.Substring(mention.Start, mention.Length)).ShouldBe(["quince", "250 euros"]);
    }

    [Theory]
    [InlineData("un documento")]
    [InlineData("una persona")]
    [InlineData("uno de los requisitos")]
    [InlineData("Sin cifras.")]
    public void Find_WhenThereIsNoNumber_ReturnsNothing(string text) =>
        NumberNormalizer.Find(text).ShouldBeEmpty();

    [Theory]
    [InlineData("15 de diciembre de 2026")]
    [InlineData("quince de diciembre de 2026")]
    [InlineData("15/12/2026")]
    [InlineData("15-12-2026")]
    [InlineData("el 15 de Diciembre del 2026")]
    public void Find_WhenTheTextHasADate_ReturnsOneDateWhateverTheFormat(string text) =>
        NumberNormalizer.Values(text).ShouldBe([NumericValue.Of(new DateOnly(2026, 12, 15))]);

    [Theory]
    [InlineData("15.12.2026")]
    [InlineData("15/12/26")]
    public void Find_ReadsDatesWithDotsOrTwoDigitYears(string text) =>
        NumberNormalizer.Values(text).ShouldBe([NumericValue.Of(new DateOnly(2026, 12, 15))]);

    [Fact]
    public void Find_WhenADateHasNoYear_KeepsTheDayAndTheMonth()
    {
        NumberNormalizer.Values("hasta el 15 de diciembre").ShouldBe([NumericValue.OfMonthDay(12, 15)]);
        NumberNormalizer.Values("hasta el 15 de noviembre").ShouldNotBe(NumberNormalizer.Values("hasta el 15 de diciembre"));
    }

    [Theory]
    [InlineData("15 000 euros", 15000)]
    [InlineData("15 mil euros", 15000)]
    [InlineData("1,5 millones de euros", 1500000)]
    [InlineData("el 40 por 100", 40)]
    [InlineData("el 40 por cien", 40)]
    [InlineData("una multa por cien euros", 100)]
    [InlineData("en el plazo de un mes", 1)]
    [InlineData("una semana", 1)]
    public void Find_ReadsOtherCommonFormats(string text, decimal expected) =>
        SingleNumber(text).ShouldBe(expected);

    [Fact]
    public void Find_WhenYJoinsTwoNumbersThatAreNotTensAndUnits_ReadsTwoFigures()
    {
        NumberNormalizer.Values("entre diez y quince días").ShouldBe([NumericValue.Of(10), NumericValue.Of(15)], ignoreOrder: true);
        NumberNormalizer.Values("treinta y uno").ShouldBe([NumericValue.Of(31)]);
    }

    [Fact]
    public void Find_WhenTheNumericDateIsImpossible_ReadsItsParts() =>
        NumberNormalizer.Values("31/02/2026").ShouldBe([NumericValue.Of(31), NumericValue.Of(2), NumericValue.Of(2026)]);

    [Fact]
    public void Find_ReadsEveryNumberOfASentence()
    {
        const string text = "Según la Ordenanza 7/2024, los ingresos no pueden superar 2,5 veces el IPREM, "
            + "la ayuda es del 40 % con un máximo de 250 € y el plazo es de quince días.";

        NumberNormalizer.Values(text).ShouldBe(
            new[] { 7m, 2024m, 2.5m, 40m, 250m, 15m }.Select(NumericValue.Of),
            ignoreOrder: true);
    }

    [Fact]
    public void Values_TreatsTheSameValueWrittenInDifferentWaysAsEqual()
    {
        NumberNormalizer.Values("quince días").ShouldBe(NumberNormalizer.Values("15 días"));
        NumberNormalizer.Values("1.500 euros").ShouldBe(NumberNormalizer.Values("1500 €"));
        NumberNormalizer.Values("2,50").ShouldBe(NumberNormalizer.Values("2,5"));
    }

    private static decimal SingleNumber(string text) =>
        NumberNormalizer.Find(text).ShouldHaveSingleItem().Value.Number.ShouldNotBeNull();
}
