// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class RomanNumeralsRuleTests
{
    private readonly RomanNumeralsRule _rule = new();

    [Theory]
    [InlineData("Lea el capítulo IV de la ley.", "IV")]
    [InlineData("Está en el anexo XII.", "XII")]
    [InlineData("Está en el título I.", "I")]
    [InlineData("Ocurrió en el siglo XXI.", "XXI")]
    [InlineData("Año MMXXVI.", "MMXXVI")]
    public void Check_WhenTextHasARomanNumeral_ReturnsIt(string text, string fragment)
    {
        var finding = _rule.Check(text).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.RomanNumerals);
        finding.Fragment.ShouldBe(fragment);
        text[finding.Start..finding.End].ShouldBe(fragment);
    }

    [Theory]
    [InlineData("Lo firma D. Juan García.")]
    [InlineData("Vive en la calle C, número 3.")]
    [InlineData("Lea el capítulo iv.")]
    [InlineData("Es el MAX de la radio.")]
    [InlineData("El capítulo IIII no existe.")]
    public void Check_WhenTextHasNoRomanNumeral_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("IV", true)]
    [InlineData("XII", true)]
    [InlineData("C", true)]
    [InlineData("BOP", false)]
    [InlineData("IIII", false)]
    [InlineData("", false)]
    public void IsRomanNumeral_RecognisesValidNumerals(string value, bool expected)
    {
        RomanNumeralsRule.IsRomanNumeral(value).ShouldBe(expected);
    }
}
