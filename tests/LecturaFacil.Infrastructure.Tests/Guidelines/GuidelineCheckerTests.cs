// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Infrastructure.Guidelines;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Tests.Guidelines;

public sealed class GuidelineCheckerTests
{
    [Fact]
    public void EmbeddedLists_AreLoadedAndHaveNoBlankEntries()
    {
        EmbeddedGuidelineLists.AllowedAcronyms().ShouldContain("OK");
        EmbeddedGuidelineLists.LegalJargon().ShouldContain(term => term.Term == "en virtud de" && term.Alternative == "según");
        EmbeddedGuidelineLists.DifficultWords().ShouldContain(term => term.Term == "arrendamiento");
        EmbeddedGuidelineLists.LegalJargon().Concat(EmbeddedGuidelineLists.DifficultWords())
            .ShouldAllBe(term => !string.IsNullOrWhiteSpace(term.Term) && term.Alternative != null);
    }

    [Fact]
    public void Check_UsesTheEmbeddedListsAndTheConfiguredLimits()
    {
        var checker = CreateChecker(new LimitsOptions { MaxWordsPerSentence = 5 });

        var codes = checker.Check("En virtud de la norma, todo está OK y pagamos el arrendamiento de la casa.")
            .Findings.Select(finding => finding.GuidelineCode).ToList();

        codes.ShouldBe(
            [GuidelineCodes.ShortSentences, GuidelineCodes.LegalJargon, GuidelineCodes.DifficultWords],
            ignoreOrder: true);
    }

    [Fact]
    public void Check_WhenTextHasNoProblems_ReturnsNoFindings()
    {
        CreateChecker(new LimitsOptions()).Check("El Ayuntamiento da ayudas. Puede pedirlas en la web.").Findings.ShouldBeEmpty();
    }

    [Fact]
    public void Check_With20000CharactersAndTheEmbeddedLists_TakesLessThan500Milliseconds()
    {
        const string paragraph =
            "La Concejalía, en virtud de lo dispuesto en la Ordenanza 7/2024, convoca ayudas para sufragar el arrendamiento. "
            + "Podrán ser beneficiarias las personas cuyos ingresos no excedan de 2,5 veces el IPREM, siempre que no sean "
            + "titulares de otra vivienda. La cuantía será del 40 % de la renta. Las solicitudes deberán presentarse en "
            + "quince días hábiles a contar desde la publicación del presente extracto en el BOP el 15/12/2026.\n";
        var text = string.Concat(Enumerable.Repeat(paragraph, 20_000 / paragraph.Length + 1))[..20_000];
        var checker = CreateChecker(new LimitsOptions());
        checker.Check(paragraph);

        var stopwatch = Stopwatch.StartNew();
        var analysis = checker.Check(text);
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500);
        analysis.Findings.ShouldNotBeEmpty();
        analysis.TimedOutGuidelines.ShouldBeEmpty();
    }

    private static GuidelineChecker CreateChecker(LimitsOptions limits) =>
        new(Options.Create(limits), NullLogger<GuidelineChecker>.Instance);
}
