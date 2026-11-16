// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Verification;
using LecturaFacil.Evals.Metrics;

namespace LecturaFacil.Evals.Tests.Metrics;

public sealed class EvalMetricsTests
{
    [Fact]
    public void GuidelineCompliance_WhenThreeOfFourGuidelinesAreCompliant_ReturnsThreeQuarters()
    {
        var report = GuidelineReport.Restore(
        [
            Result(GuidelineCodes.ShortSentences, GuidelineStatus.Compliant),
            Result(GuidelineCodes.PassiveVoice, GuidelineStatus.Compliant),
            Result(GuidelineCodes.Acronyms, GuidelineStatus.Compliant),
            Result(GuidelineCodes.DifficultWords, GuidelineStatus.NotCompliant),
        ]);

        EvalMetrics.GuidelineCompliance(report).ShouldBe(0.75);
    }

    [Fact]
    public void GuidelineCompliance_WhenReportIsMissingOrEmpty_ReturnsNull()
    {
        EvalMetrics.GuidelineCompliance(null).ShouldBeNull();
        EvalMetrics.GuidelineCompliance(GuidelineReport.Restore([])).ShouldBeNull();
    }

    [Fact]
    public void KeyFactRetention_WhenOneOfFourFactsIsChanged_ReturnsThreeQuarters()
    {
        var verification = Verification(KeyFactStatus.Conserved, KeyFactStatus.Conserved, KeyFactStatus.Conserved, KeyFactStatus.Review);

        EvalMetrics.KeyFactRetention(verification).ShouldBe(0.75);
    }

    [Fact]
    public void KeyFactRetention_WhenThereAreNoKeyFacts_ReturnsNull()
    {
        EvalMetrics.KeyFactRetention(Verification()).ShouldBeNull();
        EvalMetrics.KeyFactRetention(null).ShouldBeNull();
    }

    [Fact]
    public void KeyFactWarnings_WhenFactsAreChangedOrOmitted_CountsThem()
    {
        var verification = Verification(KeyFactStatus.Conserved, KeyFactStatus.Review, KeyFactStatus.Omitted);

        EvalMetrics.KeyFactWarnings(verification).ShouldBe(2);
        EvalMetrics.KeyFactWarnings(null).ShouldBe(0);
    }

    [Fact]
    public void CosineSimilarity_WhenVectorsAreEqual_ReturnsOne()
    {
        EvalMetrics.CosineSimilarity([1f, 2f, 3f], [1f, 2f, 3f]).ShouldBe(1, tolerance: 1e-9);
    }

    [Fact]
    public void CosineSimilarity_WhenVectorsAreOrthogonal_ReturnsZero()
    {
        EvalMetrics.CosineSimilarity([1f, 0f], [0f, 1f]).ShouldBe(0, tolerance: 1e-9);
    }

    [Fact]
    public void CosineSimilarity_WhenVectorsAreOpposite_ReturnsMinusOne()
    {
        EvalMetrics.CosineSimilarity([1f, 1f], [-1f, -1f]).ShouldBe(-1, tolerance: 1e-9);
    }

    [Fact]
    public void CosineSimilarity_WhenOneVectorIsZero_ReturnsZero()
    {
        EvalMetrics.CosineSimilarity([0f, 0f], [1f, 1f]).ShouldBe(0);
    }

    [Fact]
    public void CosineSimilarity_WhenDimensionsDiffer_Throws()
    {
        Should.Throw<ArgumentException>(() => EvalMetrics.CosineSimilarity([1f], [1f, 2f]));
    }

    [Fact]
    public void Recall_WhenFourOfFiveChangesAreDetected_ReturnsFourFifths()
    {
        EvalMetrics.Recall(detected: 4, introduced: 5).ShouldBe(0.8);
    }

    [Fact]
    public void Recall_WhenNoChangeWasIntroduced_ReturnsNull()
    {
        EvalMetrics.Recall(detected: 0, introduced: 0).ShouldBeNull();
    }

    [Fact]
    public void Recall_WhenDetectedExceedsIntroduced_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => EvalMetrics.Recall(detected: 2, introduced: 1));
    }

    [Fact]
    public void Mean_WhenSomeValuesAreMissing_IgnoresThem()
    {
        EvalMetrics.Mean([1.0, null, 0.5]).ShouldBe(0.75);
    }

    [Fact]
    public void Mean_WhenAllValuesAreMissing_ReturnsNull()
    {
        EvalMetrics.Mean([null, null]).ShouldBeNull();
        EvalMetrics.Mean([]).ShouldBeNull();
    }

    [Fact]
    public void Cost_WhenPricedPerMillionTokens_AddsInputAndOutput()
    {
        var pricing = new ModelPricing(InputPerMillion: 0.44m, OutputPerMillion: 1.76m);

        EvalMetrics.Cost(inputTokens: 2_000_000, outputTokens: 500_000, pricing).ShouldBe(1.76m);
    }

    [Fact]
    public void Cost_WhenModelIsFree_ReturnsZero()
    {
        EvalMetrics.Cost(1_000, 1_000, ModelPricing.Free).ShouldBe(0m);
    }

    [Fact]
    public void TokensPerSecond_WhenDurationIsPositive_DividesOutputByTime()
    {
        EvalMetrics.TokensPerSecond(outputTokens: 300, TimeSpan.FromSeconds(10)).ShouldBe(30);
    }

    [Fact]
    public void TokensPerSecond_WhenDurationIsZero_ReturnsNull()
    {
        EvalMetrics.TokensPerSecond(outputTokens: 300, TimeSpan.Zero).ShouldBeNull();
    }

    private static GuidelineResult Result(string code, GuidelineStatus status) =>
        new(code, status, status == GuidelineStatus.Compliant ? 0 : 1, []);

    private static MeaningVerification Verification(params KeyFactStatus[] statuses) =>
        MeaningVerification.Restore(
            VerificationStatus.Verified,
            statuses.Select((status, index) => KeyFact.Create(
                KeyFactType.Amount,
                $"dato {index}",
                status == KeyFactStatus.Omitted ? null : $"dato {index}",
                new KeyFactVerdict(status, status == KeyFactStatus.Conserved ? KeyFactReason.None : KeyFactReason.ValueChanged))),
            new VerificationMetadata("Proveedor", "modelo", "verify.v1"));
}
