// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class PercentagesRuleTests
{
    private readonly PercentagesRule _rule = new();

    [Fact]
    public void Check_WhenTextHasAPercentage_ReturnsIt()
    {
        const string text = "La ayuda será del 40 % de la renta.";

        var finding = _rule.Check(text).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.Percentages);
        finding.Fragment.ShouldBe("40 %");
        finding.Start.ShouldBe(18);
    }

    [Theory]
    [InlineData("Será del 40% de la renta.", "40%")]
    [InlineData("Será del 40 % de la renta.", "40 %")]
    [InlineData("Será del 12,5 % de la renta.", "12,5 %")]
    [InlineData("Será del 40 por ciento de la renta.", "40 por ciento")]
    [InlineData("Será del cuarenta por ciento de la renta.", "cuarenta por ciento")]
    [InlineData("Será del 100 por cien de la renta.", "100 por cien")]
    [InlineData("Se subvenciona el 40 por 100 del coste.", "40 por 100")]
    public void Check_WhenPercentageIsWrittenInAnyForm_ReturnsIt(string text, string fragment)
    {
        var findings = _rule.Check(text);

        findings.ShouldHaveSingleItem().Fragment.ShouldBe(fragment);
        findings.ShouldPointToFragments(text);
    }

    [Theory]
    [InlineData("La ayuda es de 40 euros.")]
    [InlineData("Un ciento de personas.")]
    public void Check_WhenTextHasNoPercentage_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }
}
