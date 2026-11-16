// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class ShortSentencesRuleTests
{
    private readonly ShortSentencesRule _rule = new(maxWords: 15);

    [Fact]
    public void Check_WhenSentenceHas32WordsAndThresholdIs15_SuggestsSplittingIt()
    {
        var sentence = "Esta " + string.Join(' ', Enumerable.Repeat("palabra", 31)) + ".";
        var text = "Frase corta. " + sentence;

        var finding = _rule.Check(text).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.ShortSentences);
        finding.Fragment.ShouldBe(sentence);
        finding.Start.ShouldBe(13);
        finding.Suggestion.ShouldContain("32 palabras");
        finding.Suggestion.ShouldContain("Divide");
    }

    [Fact]
    public void Check_WhenSentenceHasExactlyTheMaximum_ReturnsNothing()
    {
        var sentence = string.Join(' ', Enumerable.Repeat("palabra", 15)) + ".";

        _rule.Check(sentence).ShouldBeEmpty();
    }

    [Fact]
    public void Check_WhenSentenceHasOneMoreThanTheMaximum_ReturnsAFinding()
    {
        var sentence = string.Join(' ', Enumerable.Repeat("palabra", 16)) + ".";

        _rule.Check(sentence).ShouldHaveSingleItem();
    }

    [Fact]
    public void Check_CountsNumbersAndSymbolsCorrectly()
    {
        // 2,5 y 7/2024 son una palabra cada uno; el símbolo % no es una palabra.
        const string text = "uno dos tres cuatro cinco seis siete ocho nueve diez once doce 2,5 7/2024 40 %.";

        _rule.Check(text).ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenMaximumIsNotPositive_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ShortSentencesRule(0));
    }
}
