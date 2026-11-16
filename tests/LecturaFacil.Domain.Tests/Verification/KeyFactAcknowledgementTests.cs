// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Verification;

/// <summary>Datos clave marcados como revisados por una persona y su recálculo tras una edición manual (US-13).</summary>
public sealed class KeyFactAcknowledgementTests
{
    private const string Original = "Con un máximo de 250 €. Tiene quince días para pedirla.";

    private static readonly VerificationMetadata Verifier = new("Proveedor", "modelo-verificador", "verify.v1");

    [Fact]
    public void KeyFact_WhenItIsAWarning_RequiresAcknowledgementUntilItIsAcknowledged()
    {
        var fact = KeyFact.Create(KeyFactType.Amount, "250 €", null, new KeyFactVerdict(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation));

        fact.RequiresAcknowledgement.ShouldBeTrue();
        var acknowledged = fact.AsAcknowledged();

        acknowledged.Acknowledged.ShouldBeTrue();
        acknowledged.RequiresAcknowledgement.ShouldBeFalse();
        acknowledged.Status.ShouldBe(KeyFactStatus.Omitted);
        acknowledged.Reason.ShouldBe(KeyFactReason.NotInAdaptation);
    }

    [Fact]
    public void KeyFact_WhenItIsConserved_NeverRequiresAcknowledgement()
    {
        var fact = KeyFact.Create(KeyFactType.Amount, "250 €", "250 euros", KeyFactVerdict.Conserved);

        fact.RequiresAcknowledgement.ShouldBeFalse();
        fact.AsAcknowledged().Acknowledged.ShouldBeFalse();
    }

    [Fact]
    public void AcknowledgeKeyFact_MarksOnlyThatFactAndKeepsTheRest()
    {
        var verification = Verification(
            Fact("250 €", null, KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation),
            Fact("quince días", null, KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation));
        verification.PendingAcknowledgementCount.ShouldBe(2);

        var acknowledged = verification.AcknowledgeKeyFact(1);

        acknowledged.KeyFacts[0].Acknowledged.ShouldBeFalse();
        acknowledged.KeyFacts[1].Acknowledged.ShouldBeTrue();
        acknowledged.PendingAcknowledgementCount.ShouldBe(1);
        acknowledged.Status.ShouldBe(verification.Status);
        acknowledged.Metadata.ShouldBe(verification.Metadata);
        verification.PendingAcknowledgementCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void AcknowledgeKeyFact_WhenTheFactDoesNotExist_Throws(int index) =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Verification(Fact("250 €", null, KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)).AcknowledgeKeyFact(index));

    [Fact]
    public void RecheckAfterEdit_WhenTheEditRemovesAConservedFigure_AddsAWarning()
    {
        var verification = Verification(Fact("máximo de 250 €", "Como mucho, 250 euros", KeyFactStatus.Conserved, KeyFactReason.None));

        var rechecked = verification.RecheckAfterEdit(Original, "Le damos dinero.\nTiene 15 días.", maxKeyFacts: 50);

        var fact = rechecked.KeyFacts.Single(item => item.OriginalValue == "máximo de 250 €");
        fact.Status.ShouldBe(KeyFactStatus.Review);
        fact.Reason.ShouldBe(KeyFactReason.QuoteNotFound);
        rechecked.Status.ShouldBe(VerificationStatus.Verified);
        rechecked.Metadata.ShouldBe(Verifier);
    }

    [Fact]
    public void RecheckAfterEdit_WhenAFigureOfTheOriginalDisappears_AddsAWarningForIt()
    {
        var verification = Verification(Fact("máximo de 250 €", "Como mucho, 250 euros", KeyFactStatus.Conserved, KeyFactReason.None));

        var rechecked = verification.RecheckAfterEdit(Original, "Como mucho, 250 euros.\nTiene unos días.", maxKeyFacts: 50);

        var added = rechecked.KeyFacts.Single(item => item.Type == KeyFactType.Figure);
        added.OriginalValue.ShouldBe("quince");
        added.Status.ShouldBe(KeyFactStatus.Review);
        added.Reason.ShouldBe(KeyFactReason.FigureNotInAdaptation);
        added.RequiresAcknowledgement.ShouldBeTrue();
    }

    [Fact]
    public void RecheckAfterEdit_NeverRemovesAWarning_EvenIfTheEditFixesIt()
    {
        var verification = Verification(Fact("máximo de 250 €", "Como mucho, 300 euros", KeyFactStatus.Review, KeyFactReason.ValueChanged));

        var rechecked = verification.RecheckAfterEdit(Original, "Como mucho, 250 euros.\nTiene 15 días.", maxKeyFacts: 50);

        var fact = rechecked.KeyFacts.ShouldHaveSingleItem();
        fact.Status.ShouldBe(KeyFactStatus.Review);
        fact.Reason.ShouldBe(KeyFactReason.ValueChanged);
    }

    [Fact]
    public void RecheckAfterEdit_KeepsTheAcknowledgementOfAWarningThatDoesNotChange()
    {
        var verification = Verification(Fact("250 €", null, KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation)).AcknowledgeKeyFact(0);

        var rechecked = verification.RecheckAfterEdit(Original, "Le damos dinero.\nTiene 15 días.", maxKeyFacts: 50);

        rechecked.KeyFacts.ShouldHaveSingleItem().Acknowledged.ShouldBeTrue();
        rechecked.PendingAcknowledgementCount.ShouldBe(0);
    }

    [Fact]
    public void RecheckAfterEdit_WhenTheQuoteOfAnAcknowledgedWarningChanges_AsksForANewAcknowledgement()
    {
        var verification = Verification(
            Fact("máximo de 250 €", "Como mucho, 250 euros", KeyFactStatus.Review, KeyFactReason.CalculatedConversion)).AcknowledgeKeyFact(0);

        var rechecked = verification.RecheckAfterEdit(Original, "Como mucho, 300 euros.\nTiene 15 días.", maxKeyFacts: 50);

        var fact = rechecked.KeyFacts.Single(item => item.OriginalValue == "máximo de 250 €");
        fact.Status.ShouldBe(KeyFactStatus.Review);
        fact.Acknowledged.ShouldBeFalse();
        fact.RequiresAcknowledgement.ShouldBeTrue();
    }

    [Fact]
    public void RecheckAfterEdit_WhenItWasNotVerified_StaysNotVerified()
    {
        var verification = MeaningVerification.NotVerified(Original, 50);

        var rechecked = verification.RecheckAfterEdit(Original, "Como mucho, 250 euros.\nTiene 15 días.", maxKeyFacts: 50);

        rechecked.Status.ShouldBe(VerificationStatus.NotVerified);
        rechecked.Metadata.ShouldBeNull();
        rechecked.KeyFacts.ShouldAllBe(fact => fact.Reason == KeyFactReason.NotVerified);
    }

    private static MeaningVerification Verification(params KeyFact[] facts) =>
        MeaningVerification.Restore(VerificationStatus.Verified, facts, Verifier);

    private static KeyFact Fact(string original, string? adapted, KeyFactStatus status, KeyFactReason reason) =>
        KeyFact.Create(KeyFactType.Amount, original, adapted, new KeyFactVerdict(status, reason));
}
