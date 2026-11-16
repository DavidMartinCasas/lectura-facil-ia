// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class TermListRulesTests
{
    private readonly LegalJargonRule _jargon = new([
        new TermSuggestion("en virtud de", "según"),
        new TermSuggestion("a tenor de lo dispuesto en", "según"),
        new TermSuggestion("lo dispuesto en", "lo que dice"),
        new TermSuggestion("sin perjuicio de", ""),
    ]);

    private readonly DifficultWordsRule _words = new([
        new TermSuggestion("sufragar", "pagar"),
        new TermSuggestion("arrendamiento", "alquiler"),
    ]);

    [Fact]
    public void LegalJargon_WhenTextHasAnExpression_ReturnsItWithItsAlternative()
    {
        const string text = "Se convoca, en virtud de la ordenanza, una ayuda.";

        var finding = _jargon.Check(text).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.LegalJargon);
        finding.Fragment.ShouldBe("en virtud de");
        finding.Start.ShouldBe(12);
        finding.Suggestion.ShouldContain("según");
    }

    [Fact]
    public void LegalJargon_WhenExpressionsOverlap_PrefersTheLongestOne()
    {
        const string text = "A tenor de lo dispuesto en la ley, se convoca.";

        _jargon.Check(text).ShouldHaveSingleItem().Fragment.ShouldBe("A tenor de lo dispuesto en");
    }

    [Fact]
    public void LegalJargon_WhenExpressionSpansALineBreakOrHasExtraSpaces_ReturnsIt()
    {
        const string text = "Sin  perjuicio\nde lo anterior.";

        var finding = _jargon.Check(text).ShouldHaveSingleItem();

        finding.Fragment.ShouldBe("Sin  perjuicio\nde");
        finding.Suggestion.ShouldNotContain("«»");
    }

    [Fact]
    public void LegalJargon_WhenExpressionIsInsideAnotherWord_ReturnsNothing()
    {
        _jargon.Check("Ven virtud desde aquí.").ShouldBeEmpty();
    }

    [Fact]
    public void DifficultWords_WhenTextHasADifficultWord_ReturnsItWithASimplerOne()
    {
        const string text = "Ayudas para sufragar el Arrendamiento.";

        var findings = _words.Check(text);

        findings.Select(finding => finding.Fragment).ShouldBe(["sufragar", "Arrendamiento"]);
        findings.ShouldAllBe(finding => finding.GuidelineCode == GuidelineCodes.DifficultWords);
        findings[1].Suggestion.ShouldContain("alquiler");
    }

    [Fact]
    public void TermList_WhenEmpty_ReturnsNothing()
    {
        new DifficultWordsRule([]).Check("Ayudas para sufragar el alquiler.").ShouldBeEmpty();
    }

    [Fact]
    public void TermList_WhenATermIsBlank_Throws()
    {
        Should.Throw<ArgumentException>(() => new DifficultWordsRule([new TermSuggestion(" ", "x")]));
    }
}
