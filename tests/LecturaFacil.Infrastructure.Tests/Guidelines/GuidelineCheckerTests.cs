// SPDX-License-Identifier: Apache-2.0
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

    private static GuidelineChecker CreateChecker(LimitsOptions limits) =>
        new(Options.Create(limits), NullLogger<GuidelineChecker>.Instance);
}
