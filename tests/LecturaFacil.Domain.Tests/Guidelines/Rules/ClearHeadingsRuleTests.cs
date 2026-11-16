// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Domain.Guidelines.Rules;

namespace LecturaFacil.Domain.Tests.Guidelines.Rules;

public sealed class ClearHeadingsRuleTests
{
    private readonly ClearHeadingsRule _rule = new(maxWordsPerHeading: 6);

    [Fact]
    public void Check_WhenEverySectionHasAShortHeading_FindsNothing() =>
        _rule.Check(Content("¿Qué es esta ayuda?", "¿Quién puede pedirla?")).ShouldBeEmpty();

    [Fact]
    public void Check_WhenAHeadingIsTooLong_PointsToItInThePlainText()
    {
        const string longHeading = "¿Qué personas pueden pedir esta ayuda para pagar el alquiler?";
        var content = Content("¿Qué es esta ayuda?", longHeading);

        var finding = _rule.Check(content).ShouldHaveSingleItem();

        finding.GuidelineCode.ShouldBe(GuidelineCodes.ClearHeadings);
        finding.Fragment.ShouldBe(longHeading);
        content.PlainText[finding.Start..finding.End].ShouldBe(longHeading);
        finding.Severity.ShouldBe(GuidelineCatalog.Find(GuidelineCodes.ClearHeadings).DefaultSeverity);
    }

    [Fact]
    public void Check_WhenTheDocumentTitleIsTooLong_PointsToIt()
    {
        const string title = "Un título de documento con muchas más palabras de las que se permiten";
        var content = AdaptedContent.TryCreate(title, [new AdaptedSectionDraft("¿Qué es?", ["Una frase."])], [])!;

        var finding = _rule.Check(content).ShouldHaveSingleItem();

        finding.Start.ShouldBe(0);
        finding.Fragment.ShouldBe(title);
    }

    [Fact]
    public void Check_DoesNotLookAtTheSentences()
    {
        var content = AdaptedContent.TryCreate(
            "Ayudas",
            [new AdaptedSectionDraft("¿Qué es?", ["Una frase con muchas más palabras de las que se permiten en un título."])],
            [])!;

        _rule.Check(content).ShouldBeEmpty();
    }

    private static AdaptedContent Content(params string[] headings) =>
        AdaptedContent.TryCreate(
            "Ayudas para el alquiler",
            headings.Select(heading => new AdaptedSectionDraft(heading, ["Una frase.", "Otra frase."])),
            [])!;
}
