// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class OneIdeaPerSentenceRuleTests
{
    private readonly OneIdeaPerSentenceRule _rule = new(maxConnectors: 1);

    [Fact]
    public void Check_WhenSentenceJoinsSeveralIdeas_ReturnsTheSentence()
    {
        const string text = "Pueden pedirla las personas cuyos ingresos son bajos, siempre que no tengan otra casa.";

        var finding = _rule.Check(text).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.OneIdeaPerSentence);
        finding.Fragment.ShouldBe(text);
        finding.Suggestion.ShouldContain("cuyos");
        finding.Suggestion.ShouldContain("siempre que");
    }

    [Theory]
    [InlineData("El Ayuntamiento da ayudas para pagar el alquiler.")]
    [InlineData("Puede pedirla si vive de alquiler y gana poco.")]
    public void Check_WhenSentenceHasOneIdea_ReturnsNothing(string text)
    {
        _rule.Check(text).ShouldBeEmpty();
    }

    [Fact]
    public void Check_WhenConnectorContainsAnotherConnector_CountsItOnce()
    {
        _rule.Check("Puede pedirla siempre y cuando viva aquí.").ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenMaximumIsNotPositive_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new OneIdeaPerSentenceRule(0));
    }
}
