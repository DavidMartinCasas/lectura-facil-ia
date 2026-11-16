// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Verification;

public sealed class NumericConsistencyCheckTests
{
    private const string Adaptation = "Como mucho, 250 euros al mes.\nTiene 15 días para pedirla.\nGana menos de 1.500 euros al mes.";

    [Fact]
    public void Check_WhenTheFiguresAreTheSameWrittenDifferently_Conserves() =>
        NumericConsistencyCheck.Check("máximo de 250 €", "Como mucho, 250 euros al mes", Adaptation)
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Conserved, KeyFactReason.None));

    [Fact]
    public void Check_WhenANumberInWordsBecomesDigits_Conserves() =>
        NumericConsistencyCheck.Check("quince días hábiles", "Tiene 15 días para pedirla", Adaptation).Status
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
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Review, KeyFactReason.NotInAdaptation));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Check_WhenThereIsNoAdaptedFragment_MarksItAsOmitted(string? adapted) =>
        NumericConsistencyCheck.Check("Ordenanza Municipal 7/2024", adapted, Adaptation)
            .ShouldBe(new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation));

    [Fact]
    public void Check_WhenTheFactHasNoFigures_HasNoObjection() =>
        NumericConsistencyCheck.Check("no ser titular de otra vivienda", "No tiene otra casa a su nombre", Adaptation)
            .ShouldBe(KeyFactVerdict.Conserved);

    [Fact]
    public void Check_WhenTheAdaptationAddsAFigureThatIsInTheText_StillConserves() =>
        NumericConsistencyCheck.Check("quince días hábiles", "Tiene 15 días. Por ejemplo, 250 euros", Adaptation).Status
            .ShouldBe(KeyFactStatus.Conserved);

    [Fact]
    public void Check_ComparesDatesWrittenInDifferentFormats() =>
        NumericConsistencyCheck.Check("hasta el 15/12/2026", "hasta el 15 de diciembre de 2026", "Puede pedirla hasta el 15 de diciembre de 2026.")
            .Status.ShouldBe(KeyFactStatus.Conserved);
}
