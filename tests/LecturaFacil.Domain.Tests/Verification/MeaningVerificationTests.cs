// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Verification;

public sealed class MeaningVerificationTests
{
    private const string Original =
        "La ayuda ascenderá al 40 % de la renta mensual, con un máximo de 250 €. "
        + "Las solicitudes deberán presentarse en el plazo de quince días hábiles.";

    private const string Adaptation = "El Ayuntamiento paga una parte de su alquiler.\nComo mucho, 250 euros al mes.\nTiene 15 días para pedirla.";

    private static readonly VerificationMetadata Verifier = new("Proveedor", "modelo-verificador", "verify.v1");

    [Fact]
    public void Combine_WhenTheVerifierAndTheCodeAgree_KeepsTheVerifierFacts()
    {
        var verification = Combine(
            Fact(KeyFactType.Amount, "máximo de 250 €", "Como mucho, 250 euros al mes", KeyFactStatus.Conserved),
            Fact(KeyFactType.Deadline, "quince días", "Tiene 15 días para pedirla", KeyFactStatus.Conserved),
            Fact(KeyFactType.Amount, "40 % de la renta mensual", null, KeyFactStatus.Omitted));

        verification.Status.ShouldBe(VerificationStatus.Verified);
        verification.Metadata.ShouldBe(Verifier);
        verification.KeyFacts.Select(fact => (fact.Type, fact.Status)).ShouldBe(
            [(KeyFactType.Amount, KeyFactStatus.Omitted), (KeyFactType.Amount, KeyFactStatus.Conserved), (KeyFactType.Deadline, KeyFactStatus.Conserved)]);
    }

    [Fact]
    public void Combine_WhenTheCodeFindsAChangedFigure_OverridesTheVerifier()
    {
        var verification = Combine(
            "Con un máximo de 250 €.",
            "Como mucho, 300 euros al mes.",
            Fact(KeyFactType.Amount, "máximo de 250 €", "Como mucho, 300 euros al mes", KeyFactStatus.Conserved));

        var fact = verification.KeyFacts.ShouldHaveSingleItem();
        fact.Status.ShouldBe(KeyFactStatus.Review);
        fact.Reason.ShouldBe(KeyFactReason.ValueChanged);
        fact.ReasonMessage.ShouldBe("El valor ha cambiado");
    }

    [Fact]
    public void Combine_WhenAFigureOfTheOriginalIsMissingAndTheVerifierDidNotListIt_AddsAWarning()
    {
        var verification = Combine(
            Fact(KeyFactType.Amount, "máximo de 250 €", "Como mucho, 250 euros al mes", KeyFactStatus.Conserved),
            Fact(KeyFactType.Deadline, "quince días", "Tiene 15 días para pedirla", KeyFactStatus.Conserved));

        var added = verification.KeyFacts[0];
        added.Type.ShouldBe(KeyFactType.Figure);
        added.OriginalValue.ShouldBe("40 %");
        added.AdaptedValue.ShouldBeNull();
        added.Status.ShouldBe(KeyFactStatus.Review);
        added.ReasonMessage.ShouldBe("Esta cifra del original no está en la adaptación");
        verification.KeyFacts.Count.ShouldBe(3);
    }

    [Fact]
    public void Combine_WhenTheVerifierReturnsNothing_StillWarnsAboutEveryMissingFigure()
    {
        var verification = Combine();

        verification.KeyFacts.ShouldHaveSingleItem().OriginalValue.ShouldBe("40 %");
    }

    [Fact]
    public void Combine_DoesNotWarnAboutFiguresThatAreInTheAdaptation()
    {
        var verification = Combine("Tiene quince días.", "Tiene 15 días.");

        verification.KeyFacts.ShouldBeEmpty();
    }

    [Fact]
    public void Combine_WhenThereAreMoreFactsThanAllowed_KeepsEveryWarningAndLimitsTheConservedOnes()
    {
        var facts = Enumerable.Range(0, 5)
            .Select(_ => Fact(KeyFactType.Requirement, "no ser titular de otra vivienda", "No tiene otra casa", KeyFactStatus.Conserved))
            .Append(Fact(KeyFactType.Requirement, "estar empadronado", null, KeyFactStatus.Omitted))
            .Append(Fact(KeyFactType.Requirement, "ser mayor de edad", null, KeyFactStatus.Omitted))
            .ToArray();

        var verification = MeaningVerification.Combine(
            "No ser titular de otra vivienda, estar empadronado y ser mayor de edad.", "No tiene otra casa.", facts, Verifier, maxKeyFacts: 1);

        verification.KeyFacts.Select(fact => fact.Status).ShouldBe([KeyFactStatus.Omitted, KeyFactStatus.Omitted, KeyFactStatus.Conserved]);
    }

    [Fact]
    public void Combine_WhenTheVerifierQuotesSomethingThatIsNotInTheOriginal_AsksForReview()
    {
        var verification = Combine(
            Fact(KeyFactType.Requirement, "tener nacionalidad española", "No tiene otra casa", KeyFactStatus.Conserved));

        verification.KeyFacts.Single(fact => fact.Type == KeyFactType.Requirement).Status.ShouldBe(KeyFactStatus.Review);
    }

    [Fact]
    public void Unreadable_AsksToReviewTheWholeDocument()
    {
        var verification = MeaningVerification.Unreadable();

        verification.Status.ShouldBe(VerificationStatus.NotVerified);
        var fact = verification.KeyFacts.ShouldHaveSingleItem();
        fact.OriginalValue.ShouldBe(MeaningVerification.WholeDocument);
        fact.Reason.ShouldBe(KeyFactReason.NotVerified);
    }

    [Fact]
    public void KeyFact_Truncate_DoesNotSplitASurrogatePair()
    {
        var text = new string('x', KeyFact.ValueMaxLength - 1) + "😀";

        var truncated = KeyFact.Truncate(text);

        truncated.Length.ShouldBe(KeyFact.ValueMaxLength - 1);
        char.IsHighSurrogate(truncated[^1]).ShouldBeFalse();
    }

    [Fact]
    public void NotVerified_MarksEveryFigureOfTheOriginalForReview()
    {
        var verification = MeaningVerification.NotVerified(Original, maxKeyFacts: 50);

        verification.Status.ShouldBe(VerificationStatus.NotVerified);
        verification.Metadata.ShouldBeNull();
        verification.KeyFacts.Select(fact => fact.OriginalValue).ShouldBe(["40 %", "250 €", "quince"]);
        verification.KeyFacts.ShouldAllBe(fact => fact.Status == KeyFactStatus.Review && fact.Reason == KeyFactReason.NotVerified);
        verification.KeyFacts[0].ReasonMessage.ShouldBe("No se ha podido verificar; debe revisarlo una persona");
    }

    [Fact]
    public void NotVerified_ListsEachValueOnce() =>
        MeaningVerification.NotVerified("Tiene 15 días. Repito: 15 días.", 50).KeyFacts.ShouldHaveSingleItem();

    [Fact]
    public void Summary_CountsTheFactsByStatus()
    {
        var verification = Combine(
            Fact(KeyFactType.Amount, "máximo de 250 €", "Como mucho, 250 euros al mes", KeyFactStatus.Conserved),
            Fact(KeyFactType.Deadline, "quince días", "Tiene 15 días para pedirla", KeyFactStatus.Conserved),
            Fact(KeyFactType.Amount, "40 % de la renta mensual", null, KeyFactStatus.Omitted));

        verification.ConservedCount.ShouldBe(2);
        verification.ReviewCount.ShouldBe(0);
        verification.OmittedCount.ShouldBe(1);
        verification.Summary.ShouldBe("2 conservados, 0 a revisar, 1 omitido");
    }

    [Fact]
    public void KeyFact_WhenTheOriginalValueIsEmpty_Throws() =>
        Should.Throw<ArgumentException>(() => KeyFact.Create(KeyFactType.Other, " ", null, KeyFactVerdict.Conserved));

    [Fact]
    public void KeyFact_WhenAValueIsTooLong_Throws() =>
        Should.Throw<ArgumentException>(() =>
            KeyFact.Create(KeyFactType.Other, new string('x', KeyFact.ValueMaxLength + 1), null, KeyFactVerdict.Conserved));

    private static MeaningVerification Combine(params KeyFact[] facts) => Combine(Original, Adaptation, facts);

    private static MeaningVerification Combine(string original, string adaptation, params KeyFact[] facts) =>
        MeaningVerification.Combine(original, adaptation, facts, Verifier, maxKeyFacts: 50);

    private static KeyFact Fact(KeyFactType type, string original, string? adapted, KeyFactStatus status) =>
        KeyFact.Create(type, original, adapted, KeyFactVerdict.Create(status, KeyFactReason.None));
}
