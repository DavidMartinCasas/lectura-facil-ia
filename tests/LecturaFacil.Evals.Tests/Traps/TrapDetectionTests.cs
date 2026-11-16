// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;
using LecturaFacil.Evals.Dataset;
using LecturaFacil.Evals.Traps;

namespace LecturaFacil.Evals.Tests.Traps;

public sealed class TrapDetectionTests
{
    private static readonly Trap Trap = TrapGenerator.Create(
        new EvalText("plazo", "El plazo es de quince días.", "Tiene 15 días.", "Texto inventado", "CC0-1.0"))!;

    private static readonly MeaningVerification CleanBaseline = Verification();

    [Fact]
    public void Classify_WhenOnlyTheCodeWarnsAboutTheMissingOriginalFigure_ReturnsDetectedByCode()
    {
        var verification = Verification(Fact("quince", null, KeyFactStatus.Review, KeyFactReason.FigureNotInAdaptation));

        TrapDetection.Classify(verification, CleanBaseline, Trap).ShouldBe(TrapOutcome.DetectedByCode);
    }

    [Fact]
    public void Classify_WhenTheVerifierListsTheChangedFigure_ReturnsDetectedByVerifier()
    {
        var verification = Verification(Fact("quince días", "Tiene 16 días", KeyFactStatus.Review, KeyFactReason.ValueChanged));

        TrapDetection.Classify(verification, CleanBaseline, Trap).ShouldBe(TrapOutcome.DetectedByVerifier);
    }

    [Fact]
    public void Classify_WhenTheFigureIsMarkedAsConserved_ReturnsMissed()
    {
        var verification = Verification(Fact("quince días", "Tiene 16 días", KeyFactStatus.Conserved, KeyFactReason.None));

        TrapDetection.Classify(verification, CleanBaseline, Trap).ShouldBe(TrapOutcome.Missed);
    }

    [Fact]
    public void Classify_WhenWarningsAreAboutOtherFigures_ReturnsMissed()
    {
        var verification = Verification(Fact("250 euros", null, KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation));

        TrapDetection.Classify(verification, CleanBaseline, Trap).ShouldBe(TrapOutcome.Missed);
    }

    [Fact]
    public void Classify_WhenTheOriginalFigureWasAlreadyWarnedWithoutTheTrap_DoesNotCountThatWarning()
    {
        var warning = Fact("quince días", "Tiene 15 días", KeyFactStatus.Review, KeyFactReason.QualifierChanged);

        TrapDetection.Classify(Verification(warning), Verification(warning), Trap).ShouldBe(TrapOutcome.Missed);
    }

    [Fact]
    public void Classify_WhenAWarningQuotesTheAlteredFigure_CountsItEvenIfTheBaselineWarned()
    {
        var baseline = Verification(Fact("quince días", "Tiene 15 días", KeyFactStatus.Review, KeyFactReason.QualifierChanged));
        var verification = Verification(Fact("quince días", "Tiene 16 días", KeyFactStatus.Review, KeyFactReason.ValueChanged));

        TrapDetection.Classify(verification, baseline, Trap).ShouldBe(TrapOutcome.DetectedByVerifier);
    }

    [Fact]
    public void Classify_WhenTheVerifierDidNotAnswer_ReturnsNotVerifiedEvenThoughEveryFigureIsWarned()
    {
        var verification = MeaningVerification.NotVerified("El plazo es de quince días.", maxKeyFacts: 100);

        TrapDetection.Classify(verification, CleanBaseline, Trap).ShouldBe(TrapOutcome.NotVerified);
    }

    private static KeyFact Fact(string original, string? adapted, KeyFactStatus status, KeyFactReason reason) =>
        KeyFact.Create(KeyFactType.Deadline, original, adapted, new KeyFactVerdict(status, reason));

    private static MeaningVerification Verification(params KeyFact[] facts) =>
        MeaningVerification.Restore(VerificationStatus.Verified, facts, new VerificationMetadata("Proveedor", "modelo", "verify.v1"));
}
