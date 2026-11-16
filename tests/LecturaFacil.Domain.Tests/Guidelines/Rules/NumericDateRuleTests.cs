// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class NumericDateRuleTests
{
    private readonly NumericDateRule _rule = new();

    [Theory]
    [InlineData("El plazo acaba el 15/12/2026.", "15/12/2026", "15 de diciembre de 2026")]
    [InlineData("El plazo acaba el 1-3-2026.", "1-3-2026", "1 de marzo de 2026")]
    [InlineData("El plazo acaba el 05.06.2026.", "05.06.2026", "5 de junio de 2026")]
    [InlineData("El plazo acaba el 05/06/26.", "05/06/26", "5 de junio")]
    [InlineData("El plazo acaba el 2026-12-15.", "2026-12-15", "15 de diciembre de 2026")]
    public void Check_WhenDateIsNumeric_SuggestsTheFullDate(string text, string fragment, string suggestion)
    {
        var finding = _rule.Check(text).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.NumericDate);
        finding.Fragment.ShouldBe(fragment);
        text[finding.Start..finding.End].ShouldBe(fragment);
        finding.Suggestion.ShouldContain(suggestion);
    }

    [Theory]
    [InlineData("Según la Ordenanza 7/2024.")]
    [InlineData("El plazo acaba el 15 de diciembre de 2026.")]
    [InlineData("El código es 45/13/2026.")]
    [InlineData("El código es 32/12/2026.")]
    [InlineData("El código es 15/12-2026.")]
    [InlineData("Llame al 91.555.12.34.")]
    [InlineData("Según el apartado 3.2.10 de las bases.")]
    public void Check_WhenTextHasNoNumericDate_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }
}
