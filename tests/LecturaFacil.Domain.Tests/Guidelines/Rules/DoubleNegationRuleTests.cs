// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class DoubleNegationRuleTests
{
    private readonly DoubleNegationRule _rule = new();

    [Theory]
    [InlineData("No se admitirá nunca una solicitud tardía.", "No se admitirá nunca")]
    [InlineData("Si no presenta nada, se archiva.", "no presenta nada")]
    [InlineData("No tendrá ningún coste.", "No tendrá ningún")]
    [InlineData("No lo puede pedir nadie de fuera.", "No lo puede pedir nadie")]
    public void Check_WhenSentenceHasTwoNegations_ReturnsTheFragmentBetweenThem(string text, string fragment)
    {
        var findings = _rule.Check(text);

        var finding = findings.ShouldHaveSingleItem();
        finding.GuidelineCode.ShouldBe(GuidelineCodes.DoubleNegation);
        finding.Fragment.ShouldBe(fragment);
        findings.ShouldPointToFragments(text);
    }

    [Theory]
    [InlineData("No tiene que pagar.")]
    [InlineData("Nunca cierra.")]
    [InlineData("No tiene que pagar. Nada más.")]
    [InlineData("Nadie paga. No hace falta.")]
    public void Check_WhenNegationsAreNotInTheSameSentence_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }

    [Fact]
    public void Check_WhenSentenceHasTwoDoubleNegations_ReturnsBoth()
    {
        _rule.Check("No pagará nada y no esperará nunca.").Count.ShouldBe(2);
    }
}
