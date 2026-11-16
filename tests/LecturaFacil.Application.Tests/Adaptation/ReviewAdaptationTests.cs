// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Common;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Domain.Guidelines;
using LecturaFacil.Infrastructure.Guidelines;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Application.Tests.Adaptation;

public sealed class ReviewAdaptationTests
{
    private readonly LimitsOptions _limits = new();

    [Fact]
    public void Review_WhenTheVersionMeetsEveryGuideline_ReportsEachGuidelineAsCompliant()
    {
        var report = CreateUseCase().Execute(Content(["El Ayuntamiento le ayuda a pagar el alquiler."]));

        report.Results.ShouldAllBe(result => result.Status == GuidelineStatus.Compliant);
        report.Summary.ShouldBe($"{GuidelineCatalog.All.Count} de {GuidelineCatalog.All.Count} pautas cumplidas");
    }

    [Fact]
    public void Review_WhenTheVersionHasAPercentage_ReportsItWithItsFragmentAndItsPositionInThePlainText()
    {
        var content = Content(["La ayuda paga el 40 % del alquiler."]);

        var report = CreateUseCase().Execute(content);

        var result = report.Find(GuidelineCodes.Percentages);
        result.Status.ShouldBe(GuidelineStatus.NotCompliant);
        var finding = result.Findings.ShouldHaveSingleItem();
        finding.Fragment.ShouldBe("40 %");
        content.PlainText[finding.Start..finding.End].ShouldBe("40 %");
    }

    [Fact]
    public void Review_WhenAnAcronymIsExplainedInTheGlossary_CountsItAsCompliant()
    {
        var sentences = new[] { "El anuncio sale en el BOP." };

        CreateUseCase().Execute(Content(sentences)).Find(GuidelineCodes.Acronyms).Status.ShouldBe(GuidelineStatus.NotCompliant);
        CreateUseCase().Execute(Content(sentences, new GlossaryTermDraft("BOP", "Boletín Oficial de la Provincia.")))
            .Find(GuidelineCodes.Acronyms).Status.ShouldBe(GuidelineStatus.Compliant);
    }

    [Fact]
    public void Review_DoesNotCheckTheGlossaryDefinitions()
    {
        var report = CreateUseCase().Execute(Content(
            ["El Ayuntamiento le ayuda a pagar el alquiler."],
            new GlossaryTermDraft("Alquiler", "Lo que se paga por vivir en una casa: por ejemplo, el 40 % del sueldo.")));

        report.Find(GuidelineCodes.Percentages).Status.ShouldBe(GuidelineStatus.Compliant);
    }

    [Fact]
    public void Review_WhenASectionHeadingIsTooLong_ReportsTheClearHeadingsGuideline()
    {
        _limits.MaxWordsPerHeading = 5;
        var heading = "¿Qué personas pueden pedir esta ayuda?";
        var content = AdaptedContent.TryCreate("Ayudas", [new AdaptedSectionDraft(heading, ["Usted puede pedirla."])], [])!;

        var report = CreateUseCase().Execute(content);

        var result = report.Find(GuidelineCodes.ClearHeadings);
        result.Status.ShouldBe(GuidelineStatus.NotCompliant);
        result.Findings.ShouldHaveSingleItem().Fragment.ShouldBe(heading);
    }

    [Fact]
    public void Review_StoresAtMostTheConfiguredNumberOfFindings()
    {
        _limits.MaxFindingsPerDocument = 10;
        var content = Content([.. Enumerable.Range(1, 12).Select(index => $"Paga el {index} %.")]);

        var report = CreateUseCase().Execute(content);

        var result = report.Find(GuidelineCodes.Percentages);
        result.FindingCount.ShouldBe(12);
        result.Findings.Count.ShouldBe(10);
    }

    private static AdaptedContent Content(IReadOnlyList<string> sentences, params GlossaryTermDraft[] glossary) =>
        AdaptedContent.TryCreate("Ayudas para el alquiler", [new AdaptedSectionDraft("¿Qué es esta ayuda?", sentences)], glossary)!;

    private ReviewAdaptation CreateUseCase() =>
        new(new GuidelineChecker(Options.Create(_limits), NullLogger<GuidelineChecker>.Instance), Options.Create(_limits));
}
