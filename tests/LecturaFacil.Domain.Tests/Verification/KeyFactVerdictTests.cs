// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;

namespace LecturaFacil.Domain.Tests.Verification;

/// <summary>Regla de combinación: manda el resultado más grave; el verificador solo puede añadir avisos.</summary>
public sealed class KeyFactVerdictTests
{
    private static readonly KeyFactVerdict Conserved = KeyFactVerdict.Conserved;
    private static readonly KeyFactVerdict Changed = new(KeyFactStatus.Review, KeyFactReason.ValueChanged);
    private static readonly KeyFactVerdict Conversion = new(KeyFactStatus.Review, KeyFactReason.CalculatedConversion);
    private static readonly KeyFactVerdict Doubtful = new(KeyFactStatus.Review, KeyFactReason.NeedsReview);
    private static readonly KeyFactVerdict Omitted = new(KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation);

    [Fact]
    public void MostSevere_WhenTheCodeFindsAChangeAndTheVerifierSaysConserved_KeepsTheCodeWarning() =>
        KeyFactVerdict.MostSevere(code: Changed, verifier: Conserved).ShouldBe(Changed);

    [Fact]
    public void MostSevere_WhenTheVerifierWarnsAndTheCodeHasNoObjection_AddsTheVerifierWarning() =>
        KeyFactVerdict.MostSevere(code: Conserved, verifier: Omitted).ShouldBe(Omitted);

    [Fact]
    public void MostSevere_WhenBothAgree_Conserves() =>
        KeyFactVerdict.MostSevere(Conserved, Conserved).ShouldBe(Conserved);

    [Fact]
    public void MostSevere_OmittedIsMoreSevereThanReview()
    {
        KeyFactVerdict.MostSevere(code: Changed, verifier: Omitted).ShouldBe(Omitted);
        KeyFactVerdict.MostSevere(code: Omitted, verifier: Changed).ShouldBe(Omitted);
    }

    [Fact]
    public void MostSevere_WhenBothAskForReview_KeepsTheVerifierReasonIfItIsMoreSpecific() =>
        KeyFactVerdict.MostSevere(code: Changed, verifier: Conversion).ShouldBe(Conversion);

    [Fact]
    public void MostSevere_WhenBothAskForReview_KeepsTheCodeReasonIfTheVerifierGivesNone() =>
        KeyFactVerdict.MostSevere(code: Changed, verifier: Doubtful).ShouldBe(Changed);

    [Theory]
    [InlineData(KeyFactStatus.Conserved, KeyFactStatus.Conserved, KeyFactStatus.Conserved)]
    [InlineData(KeyFactStatus.Conserved, KeyFactStatus.Review, KeyFactStatus.Review)]
    [InlineData(KeyFactStatus.Conserved, KeyFactStatus.Omitted, KeyFactStatus.Omitted)]
    [InlineData(KeyFactStatus.Review, KeyFactStatus.Conserved, KeyFactStatus.Review)]
    [InlineData(KeyFactStatus.Review, KeyFactStatus.Review, KeyFactStatus.Review)]
    [InlineData(KeyFactStatus.Review, KeyFactStatus.Omitted, KeyFactStatus.Omitted)]
    [InlineData(KeyFactStatus.Omitted, KeyFactStatus.Conserved, KeyFactStatus.Omitted)]
    [InlineData(KeyFactStatus.Omitted, KeyFactStatus.Review, KeyFactStatus.Omitted)]
    [InlineData(KeyFactStatus.Omitted, KeyFactStatus.Omitted, KeyFactStatus.Omitted)]
    public void MostSevere_NeverEndsLessSevereThanTheCode(KeyFactStatus code, KeyFactStatus verifier, KeyFactStatus expected)
    {
        var result = KeyFactVerdict.MostSevere(Verdict(code), Verdict(verifier));

        result.Status.ShouldBe(expected);
        ((int)result.Status).ShouldBeGreaterThanOrEqualTo((int)code);
    }

    [Fact]
    public void Create_WhenConservedComesWithAWarningReason_AsksForReview() =>
        KeyFactVerdict.Create(KeyFactStatus.Conserved, KeyFactReason.ValueChanged).ShouldBe(new(KeyFactStatus.Review, KeyFactReason.ValueChanged));

    [Fact]
    public void Create_WhenAWarningHasNoReason_UsesTheGenericOne() =>
        KeyFactVerdict.Create(KeyFactStatus.Omitted, KeyFactReason.None).ShouldBe(new(KeyFactStatus.Omitted, KeyFactReason.NeedsReview));

    [Fact]
    public void Create_WhenConservedWithoutReason_Conserves() =>
        KeyFactVerdict.Create(KeyFactStatus.Conserved, KeyFactReason.None).ShouldBe(Conserved);

    private static KeyFactVerdict Verdict(KeyFactStatus status) => KeyFactVerdict.Create(status, KeyFactReason.None);
}
