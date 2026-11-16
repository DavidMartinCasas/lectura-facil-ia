// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class NumbersInDigitsRuleTests
{
    private readonly NumbersInDigitsRule _rule = new();

    [Fact]
    public void Check_WhenNumberIsWrittenInWords_ReturnsIt()
    {
        const string text = "Tiene un plazo de quince días.";

        var finding = _rule.Check(text).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.NumbersInDigits);
        finding.Fragment.ShouldBe("quince");
        finding.Start.ShouldBe(18);
    }

    [Theory]
    [InlineData("Son treinta y dos personas.", "treinta y dos")]
    [InlineData("Cuesta dos mil euros.", "dos mil")]
    [InlineData("Hay Veinte plazas.", "Veinte")]
    public void Check_WhenNumberHasSeveralWords_ReturnsItAsOneFinding(string text, string fragment)
    {
        _rule.Check(text).ShouldHaveSingleItem().Fragment.ShouldBe(fragment);
    }

    [Theory]
    [InlineData("Tiene un plazo de 15 días.")]
    [InlineData("Una persona puede pedir una ayuda.")]
    [InlineData("Ayuda del 40 por ciento.")]
    [InlineData("Las docenas no cuentan; tampoco los doscientosmil.")]
    public void Check_WhenTextHasNoNumbersInWords_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }
}
