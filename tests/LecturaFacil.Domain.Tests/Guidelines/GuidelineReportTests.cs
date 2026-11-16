// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Tests.Guidelines;

public sealed class GuidelineReportTests
{
    private static readonly GuidelineAnalyzer Analyzer = new(GuidelineRules.Create(new GuidelineRuleSettings(
        MaxWordsPerSentence: 15,
        MaxConnectorsPerSentence: 1,
        AllowedAcronyms: ["DNI"],
        LegalJargon: [new TermSuggestion("en virtud de", "por")],
        DifficultWords: [new TermSuggestion("subsanar", "corregir")])));

    private const int MaxFindings = 500;

    [Fact]
    public void Create_WhenTheTextBreaksNoGuideline_MarksEveryGuidelineAsCompliant()
    {
        var report = Review("¿Qué es esta ayuda?\nEl Ayuntamiento le ayuda a pagar el alquiler.");

        report.Results.Select(result => result.GuidelineCode).ShouldBe(GuidelineCatalog.All.Select(guideline => guideline.Code));
        report.Results.ShouldAllBe(result => result.Status == GuidelineStatus.Compliant && result.Findings.Count == 0);
        report.CompliantCount.ShouldBe(GuidelineCatalog.All.Count);
        report.TotalCount.ShouldBe(GuidelineCatalog.All.Count);
        report.Summary.ShouldBe($"{GuidelineCatalog.All.Count} de {GuidelineCatalog.All.Count} pautas cumplidas");
        report.HasHighSeverityViolations.ShouldBeFalse();
    }

    [Fact]
    public void Create_WhenTheTextHasAPercentage_MarksThatGuidelineAsNotCompliantWithItsFragmentAndPosition()
    {
        const string text = "La ayuda paga el 40 % del alquiler.";

        var report = Review(text);

        var result = report.Find(GuidelineCodes.Percentages);
        result.Status.ShouldBe(GuidelineStatus.NotCompliant);
        result.FindingCount.ShouldBe(1);
        var finding = result.Findings.ShouldHaveSingleItem();
        finding.Fragment.ShouldBe("40 %");
        finding.Start.ShouldBe(text.IndexOf("40 %", StringComparison.Ordinal));
        finding.End.ShouldBe(finding.Start + "40 %".Length);
        report.CompliantCount.ShouldBe(GuidelineCatalog.All.Count - 1);
        report.Summary.ShouldBe($"{GuidelineCatalog.All.Count - 1} de {GuidelineCatalog.All.Count} pautas cumplidas");
    }

    [Fact]
    public void Create_WhenAnAcronymIsInTheGlossary_CountsItAsExplained()
    {
        const string text = "El anuncio sale en el BOP.";

        Review(text).Find(GuidelineCodes.Acronyms).Status.ShouldBe(GuidelineStatus.NotCompliant);
        Review(text, "BOP").Find(GuidelineCodes.Acronyms).Status.ShouldBe(GuidelineStatus.Compliant);
    }

    [Theory]
    [InlineData("bop")]
    [InlineData("BOP (Boletín Oficial de la Provincia)")]
    [InlineData("Boletín Oficial de la Provincia (BOP)")]
    public void Create_WhenTheGlossaryTermContainsTheAcronymAsAWord_CountsItAsExplained(string term) =>
        Review("El anuncio sale en el BOP.", term).Find(GuidelineCodes.Acronyms).Status.ShouldBe(GuidelineStatus.Compliant);

    [Fact]
    public void Create_WhenTheGlossaryTermOnlyContainsTheAcronymInsideAnotherWord_DoesNotCountItAsExplained() =>
        Review("El anuncio sale en el BOP.", "BOPA").Find(GuidelineCodes.Acronyms).Status.ShouldBe(GuidelineStatus.NotCompliant);

    [Fact]
    public void Create_WhenADifficultWordIsInTheGlossary_CountsItAsExplained()
    {
        const string text = "Puede subsanar la solicitud.";

        Review(text).Find(GuidelineCodes.DifficultWords).Status.ShouldBe(GuidelineStatus.NotCompliant);
        Review(text, "Subsanar").Find(GuidelineCodes.DifficultWords).Status.ShouldBe(GuidelineStatus.Compliant);
    }

    [Fact]
    public void Create_TheGlossaryDoesNotExplainOtherGuidelines()
    {
        var report = Review("La ayuda paga el 40 % del alquiler.", "40 %");

        report.Find(GuidelineCodes.Percentages).Status.ShouldBe(GuidelineStatus.NotCompliant);
    }

    [Fact]
    public void Create_WhenAGuidelineTimedOut_MarksItAsNotCheckedAndNotAsCompliant()
    {
        var analysis = new GuidelineAnalysis([], [GuidelineCodes.PassiveVoice]);

        var report = GuidelineReport.Create(analysis, [], MaxFindings);

        report.Find(GuidelineCodes.PassiveVoice).Status.ShouldBe(GuidelineStatus.NotChecked);
        report.CompliantCount.ShouldBe(GuidelineCatalog.All.Count - 1);
    }

    [Fact]
    public void Create_WhenThereAreMoreFindingsThanTheLimit_KeepsTheFirstOnesAndTheRealCounts()
    {
        var text = string.Join(' ', Enumerable.Range(1, 5).Select(index => $"Paga el {index} %."));

        var report = GuidelineReport.Create(Analyzer.Analyze(text), [], maxStoredFindings: 2);

        var result = report.Find(GuidelineCodes.Percentages);
        result.Status.ShouldBe(GuidelineStatus.NotCompliant);
        result.FindingCount.ShouldBe(5);
        result.Findings.Select(finding => finding.Fragment).ShouldBe(["1 %", "2 %"]);
    }

    [Fact]
    public void Create_CountsTheHighSeverityFindings()
    {
        var report = Review("No viene nunca. En virtud de la ley, paga el 40 %.");

        report.HasHighSeverityViolations.ShouldBeTrue();
        report.HighSeverityFindingCount.ShouldBe(2);
        report.FindingCount.ShouldBe(3);
    }

    [Fact]
    public void Violations_ListsTheGuidelinesNotMetFromTheMostToTheLeastSevere()
    {
        var report = Review("Paga el 40 %. No viene nunca.");

        report.Violations.Select(result => result.GuidelineCode)
            .ShouldBe([GuidelineCodes.DoubleNegation, GuidelineCodes.Percentages]);
    }

    [Fact]
    public void Restore_KeepsTheStoredResults()
    {
        var original = Review("Paga el 40 %.");

        var restored = GuidelineReport.Restore(original.Results);

        restored.Results.ShouldBe(original.Results);
        restored.Summary.ShouldBe(original.Summary);
    }

    private static GuidelineReport Review(string text, params string[] glossary) =>
        GuidelineReport.Create(Analyzer.Analyze(text), glossary, MaxFindings);
}
