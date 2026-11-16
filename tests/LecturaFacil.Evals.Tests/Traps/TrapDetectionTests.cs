// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Verification;
using LecturaFacil.Evals.Dataset;
using LecturaFacil.Evals.Traps;

namespace LecturaFacil.Evals.Tests.Traps;

public sealed class TrapDetectionTests
{
    private static readonly Trap Trap = TrapGenerator.Create(
        new EvalText("plazo", "El plazo es de quince días.", "Tiene 15 días.", "Texto inventado", "CC0-1.0"))!;

    [Fact]
    public void IsDetected_WhenAWarningQuotesTheOriginalFigure_ReturnsTrue()
    {
        var verification = Verification(Fact("quince días", null, KeyFactStatus.Omitted, KeyFactReason.FigureNotInAdaptation));

        TrapDetection.IsDetected(verification, Trap).ShouldBeTrue();
    }

    [Fact]
    public void IsDetected_WhenAWarningQuotesTheAlteredFigure_ReturnsTrue()
    {
        var verification = Verification(Fact("plazo", "Tiene 16 días", KeyFactStatus.Review, KeyFactReason.ValueChanged));

        TrapDetection.IsDetected(verification, Trap).ShouldBeTrue();
    }

    [Fact]
    public void IsDetected_WhenTheFigureIsMarkedAsConserved_ReturnsFalse()
    {
        var verification = Verification(Fact("quince días", "Tiene 16 días", KeyFactStatus.Conserved, KeyFactReason.None));

        TrapDetection.IsDetected(verification, Trap).ShouldBeFalse();
    }

    [Fact]
    public void IsDetected_WhenWarningsAreAboutOtherFigures_ReturnsFalse()
    {
        var verification = Verification(Fact("250 euros", null, KeyFactStatus.Omitted, KeyFactReason.NotInAdaptation));

        TrapDetection.IsDetected(verification, Trap).ShouldBeFalse();
    }

    [Fact]
    public void IsDetected_WhenThereIsNoWarning_ReturnsFalse()
    {
        TrapDetection.IsDetected(Verification(), Trap).ShouldBeFalse();
    }

    private static KeyFact Fact(string original, string? adapted, KeyFactStatus status, KeyFactReason reason) =>
        KeyFact.Create(KeyFactType.Deadline, original, adapted, new KeyFactVerdict(status, reason));

    private static MeaningVerification Verification(params KeyFact[] facts) =>
        MeaningVerification.Restore(VerificationStatus.Verified, facts, new VerificationMetadata("Proveedor", "modelo", "verify.v1"));
}
