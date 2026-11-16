// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Domain.Tests.Readability;

public sealed class ReadabilityScoreTests
{
    [Fact]
    public void FromValue_WhenValueIs72_IsQuiteEasy()
    {
        var score = ReadabilityScore.FromValue(72);

        score.Grade.ShouldBe(ReadabilityGrade.QuiteEasy);
        score.GradeLabel.ShouldBe("Bastante fácil");
    }

    [Theory]
    [InlineData(-10, ReadabilityGrade.VeryDifficult)]
    [InlineData(39.99, ReadabilityGrade.VeryDifficult)]
    [InlineData(40, ReadabilityGrade.SomewhatDifficult)]
    [InlineData(54.99, ReadabilityGrade.SomewhatDifficult)]
    [InlineData(55, ReadabilityGrade.Normal)]
    [InlineData(64.99, ReadabilityGrade.Normal)]
    [InlineData(65, ReadabilityGrade.QuiteEasy)]
    [InlineData(80, ReadabilityGrade.QuiteEasy)]
    [InlineData(80.01, ReadabilityGrade.VeryEasy)]
    [InlineData(120, ReadabilityGrade.VeryEasy)]
    public void FromValue_WhenValueIsAtOrNearABoundary_UsesTheInfleszScale(double value, ReadabilityGrade expected)
    {
        ReadabilityScore.FromValue(value).Grade.ShouldBe(expected);
    }

    [Theory]
    [InlineData(ReadabilityGrade.VeryDifficult, "Muy difícil")]
    [InlineData(ReadabilityGrade.SomewhatDifficult, "Algo difícil")]
    [InlineData(ReadabilityGrade.Normal, "Normal")]
    [InlineData(ReadabilityGrade.QuiteEasy, "Bastante fácil")]
    [InlineData(ReadabilityGrade.VeryEasy, "Muy fácil")]
    public void GradeLabel_WhenGradeIsKnown_ReturnsTheSpanishName(ReadabilityGrade grade, string expected)
    {
        ReadabilityScore.LabelOf(grade).ShouldBe(expected);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FromValue_WhenValueIsNotFinite_Throws(double value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ReadabilityScore.FromValue(value));
    }

    [Fact]
    public void LabelOf_WhenGradeIsUnknown_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ReadabilityScore.LabelOf((ReadabilityGrade)99));
    }
}
