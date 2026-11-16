// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Readability;
using LecturaFacil.Domain.Readability;

namespace LecturaFacil.Application.Tests.Readability;

public sealed class CompareReadabilityTests
{
    private const string Original = "La persona solicitante deberá presentar la documentación acreditativa.";
    private const string Adapted = "Tienes que dar los papeles.";

    private readonly ScoresByText _calculator = new()
    {
        [Original] = ReadabilityScore.FromValue(30),
        [Adapted] = ReadabilityScore.FromValue(72),
    };

    [Fact]
    public void Compare_WhenBothTextsHaveWords_ReturnsTheScoreOfTheOriginalAndOfTheAdaptation()
    {
        var result = new CompareReadability(_calculator).Compare(Original, Adapted);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Original.Value.ShouldBe(30);
        result.Value.Adapted.Value.ShouldBe(72);
        result.Value.Adapted.GradeLabel.ShouldBe("Bastante fácil");
        result.Value.Improvement.ShouldBe(42);
    }

    [Theory]
    [InlineData("", Adapted)]
    [InlineData(Original, "")]
    public void Compare_WhenATextHasNoWords_ReturnsNoWordsError(string original, string adapted)
    {
        var result = new CompareReadability(_calculator).Compare(original, adapted);

        result.Error.ShouldBe(ReadabilityErrors.NoWords);
    }

    [Fact]
    public void Compare_WhenATextIsNull_Throws()
    {
        var useCase = new CompareReadability(_calculator);

        Should.Throw<ArgumentNullException>(() => useCase.Compare(null!, Adapted));
        Should.Throw<ArgumentNullException>(() => useCase.Compare(Original, null!));
    }

    private sealed class ScoresByText : Dictionary<string, ReadabilityScore>, IReadabilityCalculator
    {
        public Result<ReadabilityScore> Calculate(string text) => TryGetValue(text, out var score)
            ? Result<ReadabilityScore>.Success(score)
            : Result<ReadabilityScore>.Failure(ReadabilityErrors.NoWords);
    }
}
