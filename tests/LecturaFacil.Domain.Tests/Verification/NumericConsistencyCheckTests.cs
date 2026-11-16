// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Verification;

public sealed class NumericConsistencyCheckTests
{
    private const string Adaptation =
        "Como mucho, 250 euros al mes.\nTiene 15 días para pedirla.\nNo cuentan sábados, domingos ni festivos.\nGana menos de 1.500 euros al mes.";

    [Fact]
    public void Check_WhenTheFiguresAreTheSameWrittenDifferently_Conserves() =>
        NumericConsistencyCheck.Check("máximo de 250 €", "Como mucho, 250 euros al mes", Adaptation)
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Conserved, KeyFactReason.None));

    [Fact]
    public void Check_WhenANumberInWordsBecomesDigits_Conserves() =>
        NumericConsistencyCheck.Check(
                "quince días hábiles", "Tiene 15 días para pedirla. No cuentan sábados, domingos ni festivos", Adaptation).Status
            .ShouldBe(KeyFactStatus.Conserved);

    [Fact]
    public void Check_WhenTheValueChanges_AsksForReview() =>
        NumericConsistencyCheck.Check("quince días hábiles", "10 días", "Tiene 10 días para pedirla.")
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.ValueChanged));

    [Fact]
    public void Check_WhenAReferenceBecomesAnAmount_AsksForReview() =>
        NumericConsistencyCheck.Check("2,5 veces el IPREM", "Gana menos de 1.500 euros al mes", Adaptation)
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.ValueChanged));

    [Fact]
    public void Check_WhenTheQuotedFigureIsNotInTheAdaptation_AsksForReview() =>
        NumericConsistencyCheck.Check("máximo de 250 €", "Como mucho, 250 euros al mes", "Como mucho, 300 euros al mes.")
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.QuoteNotFound));

    [Fact]
    public void Check_WhenTheQuoteIsNotLiterallyInTheAdaptation_AsksForReview() =>
        NumericConsistencyCheck.Check("máximo de 250 €", "Como mucho, 250 euros al mes", "Al menos 250 euros al año. Como mucho, 300 euros.")
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.QuoteNotFound));

    [Fact]
    public void Check_IgnoresLineBreaksAndCaseInTheQuote() =>
        NumericConsistencyCheck.Check("quince días", "tiene 15 días para pedirla. no cuentan", Adaptation).Status
            .ShouldBe(KeyFactStatus.Conserved);

    [Theory]
    [InlineData("quince días hábiles", "Tiene 15 días naturales", "Tiene 15 días naturales.")]
    [InlineData("quince días hábiles", "Tiene 15 días para pedirla", Adaptation)]
    [InlineData("15 días", "15 meses", "Tiene 15 meses.")]
    [InlineData("250 € al mes", "250 euros al año", "Le dan 250 euros al año.")]
    [InlineData("máximo de 250 €", "al menos 250 euros", "Le dan al menos 250 euros.")]
    [InlineData("el 40 % de la renta", "40 euros", "Le dan 40 euros.")]
    public void Check_WhenWhatGoesWithTheFigureChanges_AsksForReview(string original, string adapted, string adaptation) =>
        NumericConsistencyCheck.Check(original, adapted, adaptation)
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.QualifierChanged));

    [Theory]
    [InlineData("hasta el 15 de diciembre", "hasta el 15 de noviembre", "Puede pedirla hasta el 15 de noviembre.")]
    [InlineData("en el plazo de un mes", "tiene 2 meses", "Usted tiene 2 meses.")]
    public void Check_WhenADateOrAPeriodChanges_AsksForReview(string original, string adapted, string adaptation) =>
        NumericConsistencyCheck.Check(original, adapted, adaptation).Reason.ShouldBe(KeyFactReason.ValueChanged);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Check_WhenThereIsNoAdaptedFragment_MarksItAsOmitted(string? adapted) =>
        NumericConsistencyCheck.Check("Ordenanza Municipal 7/2024", adapted, Adaptation)
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation));

    [Fact]
    public void Check_WhenTheFactHasNoFigures_HasNoObjection() =>
        NumericConsistencyCheck.Check("no ser titular de otra vivienda", "No tiene otra casa a su nombre", "No tiene otra casa a su nombre.")
            .ShouldBe(KeyFactVerdict.Conserved);

    [Fact]
    public void Check_WhenTheQuoteOfAFactWithoutFiguresIsInvented_AsksForReview() =>
        NumericConsistencyCheck.Check("no ser titular de otra vivienda", "No tiene otra casa a su nombre", Adaptation)
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.QuoteNotFound));

    [Fact]
    public void Check_WhenTheAdaptationAddsAFigureThatIsInTheText_StillConserves() =>
        NumericConsistencyCheck.Check(
                "quince días", "Como mucho, 250 euros al mes. Tiene 15 días", Adaptation).Status
            .ShouldBe(KeyFactStatus.Conserved);

    [Fact]
    public void Check_ComparesDatesWrittenInDifferentFormats() =>
        NumericConsistencyCheck.Check("hasta el 15/12/2026", "hasta el 15 de diciembre de 2026", "Puede pedirla hasta el 15 de diciembre de 2026.")
            .Status.ShouldBe(KeyFactStatus.Conserved);
}
