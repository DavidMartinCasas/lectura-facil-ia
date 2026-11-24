// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Verification;

public sealed class MeaningVerificationPartsTests
{
    private const string Part1 = "La ayuda es de 250 € al mes.";
    private const string Part2 = "Se pide en el plazo de quince días.";
    private const string Part3 = "Se paga durante 12 meses.";

    private static readonly VerificationMetadata Verifier = new("Proveedor", "modelo-verificador", "verify.v1");

    [Fact]
    public void CombineParts_JoinsTheKeyFactsOfEveryPartFromMostToLeastSevere()
    {
        var verification = MeaningVerification.CombineParts(
            [
                VerificationPart.Verified(Part1, [Fact(KeyFactType.Amount, "250 €", "250 euros al mes", KeyFactStatus.Conserved)], Verifier),
                VerificationPart.Verified(Part2, [Fact(KeyFactType.Deadline, "quince días", null, KeyFactStatus.Omitted)], Verifier),
            ],
            "Le dan 250 euros al mes.\nTiene que pedirla pronto.",
            maxKeyFacts: 50);

        verification.Status.ShouldBe(VerificationStatus.Verified);
        verification.Metadata.ShouldBe(Verifier);
        verification.KeyFacts.Select(fact => (fact.OriginalValue, fact.Status)).ShouldBe(
            [("quince días", KeyFactStatus.Omitted), ("250 €", KeyFactStatus.Conserved)]);
    }

    [Fact]
    public void CombineParts_ChecksTheQuoteOfEachFactAgainstItsOwnPartAndTheWholeAdaptation()
    {
        // El dato de la parte 2 cita el original de la parte 2 y un fragmento de la adaptación que existe.
        var verification = MeaningVerification.CombineParts(
            [
                VerificationPart.Verified(Part1, [Fact(KeyFactType.Amount, "250 €", "250 euros al mes", KeyFactStatus.Conserved)], Verifier),
                VerificationPart.Verified(Part2, [Fact(KeyFactType.Deadline, "quince días", "Tiene 15 días", KeyFactStatus.Conserved)], Verifier),
            ],
            "Le dan 250 euros al mes.\nTiene 15 días para pedirla.",
            maxKeyFacts: 50);

        verification.KeyFacts.ShouldAllBe(fact => fact.Status == KeyFactStatus.Conserved);
    }

    [Fact]
    public void CombineParts_WhenAQuoteIsNotInItsPart_AsksToReviewIt()
    {
        // "250 €" es de la parte 1: si el verificador de la parte 2 dice que es suyo, se lo ha inventado.
        var verification = MeaningVerification.CombineParts(
            [
                VerificationPart.Verified(Part1, [], Verifier),
                VerificationPart.Verified(Part2, [Fact(KeyFactType.Amount, "250 €", "250 euros al mes", KeyFactStatus.Conserved)], Verifier),
            ],
            "Le dan 250 euros al mes.\nTiene 15 días para pedirla.",
            maxKeyFacts: 50);

        verification.KeyFacts.ShouldContain(fact => fact.OriginalValue == "250 €" && fact.Status == KeyFactStatus.Review);
    }

    [Fact]
    public void CombineParts_WarnsAboutAFigureOfTheOriginalThatIsInNoAdaptedPart()
    {
        var verification = MeaningVerification.CombineParts(
            [
                VerificationPart.Verified(Part1, [], Verifier),
                VerificationPart.Verified(Part3, [], Verifier),
            ],
            // 250 aparece en la adaptación aunque el verificador no lo haya listado; 12 no aparece en ninguna parte.
            "Le dan 250 euros al mes.\nSe paga durante un año.",
            maxKeyFacts: 50);

        var warning = verification.KeyFacts.ShouldHaveSingleItem();
        warning.OriginalValue.ShouldBe("12");
        warning.Reason.ShouldBe(KeyFactReason.FigureNotInAdaptation);
    }

    [Fact]
    public void CombineParts_WhenAPartFails_KeepsTheOthersAndMarksThatPartForReview()
    {
        var verification = MeaningVerification.CombineParts(
            [
                VerificationPart.Verified(Part1, [Fact(KeyFactType.Amount, "250 €", "250 euros al mes", KeyFactStatus.Conserved)], Verifier),
                VerificationPart.Failed(Part2),
                VerificationPart.Verified(Part3, [Fact(KeyFactType.Deadline, "12 meses", "12 meses", KeyFactStatus.Conserved)], Verifier),
            ],
            "Le dan 250 euros al mes.\nTiene 15 días.\nSe paga durante 12 meses.",
            maxKeyFacts: 50);

        verification.Status.ShouldBe(VerificationStatus.PartiallyVerified);
        var part = verification.KeyFacts.Where(fact => fact.Reason == KeyFactReason.NotVerified && fact.Type == KeyFactType.Other).ShouldHaveSingleItem();
        part.OriginalValue.ShouldBe("Parte 2");
        part.Status.ShouldBe(KeyFactStatus.Review);
        part.ReasonMessage.ShouldBe("No se ha podido verificar; debe revisarlo una persona");
        verification.KeyFacts.Where(fact => fact.Status == KeyFactStatus.Conserved).Select(fact => fact.OriginalValue)
            .ShouldBe(["250 €", "12 meses"], ignoreOrder: true);
    }

    [Fact]
    public void CombineParts_WhenAFailedPartHasFigures_MarksThemForReviewToo()
    {
        var verification = MeaningVerification.CombineParts(
            [VerificationPart.Verified(Part1, [], Verifier), VerificationPart.Failed(Part3)],
            "Le dan 250 euros al mes.\nSe paga durante 12 meses.",
            maxKeyFacts: 50);

        verification.KeyFacts.Where(fact => fact.Reason == KeyFactReason.NotVerified).Select(fact => fact.OriginalValue)
            .ShouldBe(["Parte 2", "12"], ignoreOrder: true);
    }

    [Fact]
    public void CombineParts_WhenEveryPartFails_IsNotVerified()
    {
        var verification = MeaningVerification.CombineParts(
            [VerificationPart.Failed(Part1), VerificationPart.Failed(Part3)],
            "Le dan 250 euros al mes.",
            maxKeyFacts: 50);

        verification.Status.ShouldBe(VerificationStatus.NotVerified);
        verification.Metadata.ShouldBeNull();
    }

    [Fact]
    public void CombineParts_WhenTheSameKeyFactIsInTwoParts_KeepsItOnceWithTheMostSevereVerdict()
    {
        const string repeated = "La ayuda es de 250 € al mes. Recuerde: 250 € al mes.";
        var verification = MeaningVerification.CombineParts(
            [
                VerificationPart.Verified(Part1, [Fact(KeyFactType.Amount, "250 €", "250 euros al mes", KeyFactStatus.Conserved)], Verifier),
                VerificationPart.Verified(repeated, [Fact(KeyFactType.Amount, "250 €", null, KeyFactStatus.Omitted)], Verifier),
            ],
            "Le dan 250 euros al mes.",
            maxKeyFacts: 50);

        var fact = verification.KeyFacts.ShouldHaveSingleItem();
        fact.Status.ShouldBe(KeyFactStatus.Omitted);
    }

    [Fact]
    public void CombineParts_WhenTheSameFigureIsMissingInTwoParts_WarnsOnce()
    {
        var verification = MeaningVerification.CombineParts(
            [VerificationPart.Verified(Part3, [], Verifier), VerificationPart.Verified("Y otros 12 meses más.", [], Verifier)],
            "Se paga durante un año.",
            maxKeyFacts: 50);

        verification.KeyFacts.ShouldHaveSingleItem().OriginalValue.ShouldBe("12");
    }

    [Fact]
    public void CombineParts_WithASinglePart_IsTheSameAsCombine()
    {
        var facts = new[] { Fact(KeyFactType.Amount, "250 €", "250 euros al mes", KeyFactStatus.Conserved) };
        const string adaptation = "Le dan 250 euros al mes.";

        var parts = MeaningVerification.CombineParts([VerificationPart.Verified(Part1, facts, Verifier)], adaptation, 50);
        var whole = MeaningVerification.Combine(Part1, adaptation, facts, Verifier, 50);

        parts.Status.ShouldBe(whole.Status);
        parts.KeyFacts.ShouldBe(whole.KeyFacts);
    }

    [Fact]
    public void CombineParts_WithASingleFailedPart_IsTheSameAsNotVerified()
    {
        var parts = MeaningVerification.CombineParts([VerificationPart.Failed(Part3)], "Se paga un año.", 50);

        parts.Status.ShouldBe(VerificationStatus.NotVerified);
        parts.KeyFacts.ShouldBe(MeaningVerification.NotVerified(Part3, 50).KeyFacts);
    }

    [Fact]
    public void CombineParts_WithoutParts_Throws() =>
        Should.Throw<ArgumentException>(() => MeaningVerification.CombineParts([], "Texto.", 50));

    private static KeyFact Fact(KeyFactType type, string original, string? adapted, KeyFactStatus status) =>
        KeyFact.Create(type, original, adapted, KeyFactVerdict.Create(status, KeyFactReason.None));
}
