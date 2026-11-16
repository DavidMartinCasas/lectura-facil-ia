// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Readability;
using LecturaFacil.Domain.Readability;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Infrastructure.Tests.Readability;

public sealed class SzigrisztPazosReadabilityCalculatorTests
{
    [Fact]
    public void Calculate_WhenTextHasWords_ReturnsItsScore()
    {
        var result = Calculator().Calculate("El perro come. La casa es grande.");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(105.435, 1);
        result.Value.Grade.ShouldBe(ReadabilityGrade.VeryEasy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  123 ... 456  ")]
    public void Calculate_WhenTextHasNoWords_ReturnsNoWordsError(string text)
    {
        Calculator().Calculate(text).Error.ShouldBe(ReadabilityErrors.NoWords);
    }

    [Fact]
    public void Calculate_WhenTextIsNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => Calculator().Calculate(null!));
    }

    private static IReadabilityCalculator Calculator()
    {
        using var services = new ServiceCollection().AddInfrastructure().BuildServiceProvider();
        return services.GetRequiredService<IReadabilityCalculator>();
    }
}
