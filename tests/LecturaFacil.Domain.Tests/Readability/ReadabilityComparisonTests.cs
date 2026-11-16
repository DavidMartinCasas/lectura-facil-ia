// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Domain.Tests.Readability;

public sealed class ReadabilityComparisonTests
{
    [Fact]
    public void Improvement_WhenAdaptationIsEasier_IsTheDifferenceBetweenBothScores()
    {
        var comparison = new ReadabilityComparison(ReadabilityScore.FromValue(45.5), ReadabilityScore.FromValue(72));

        comparison.Original.Grade.ShouldBe(ReadabilityGrade.SomewhatDifficult);
        comparison.Adapted.Grade.ShouldBe(ReadabilityGrade.QuiteEasy);
        comparison.Improvement.ShouldBe(26.5, 0.0001);
    }

    [Fact]
    public void Constructor_WhenAScoreIsMissing_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ReadabilityComparison(null!, ReadabilityScore.FromValue(72)));
        Should.Throw<ArgumentNullException>(() => new ReadabilityComparison(ReadabilityScore.FromValue(72), null!));
    }
}
