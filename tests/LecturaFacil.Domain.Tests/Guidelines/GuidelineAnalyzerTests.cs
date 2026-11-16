// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics;
using System.Text.RegularExpressions;
using LecturaFacil.Domain.Guidelines;

namespace LecturaFacil.Domain.Tests.Guidelines;

public sealed class GuidelineAnalyzerTests
{
    /// <summary>Texto original del ejemplo de la sección 5 de docs/TFM-Lectura-Facil.md.</summary>
    private const string ReferenceExample =
        "La Concejalía de Vivienda, en virtud de lo dispuesto en la Ordenanza Municipal 7/2024, convoca ayudas destinadas a "
        + "sufragar parcialmente el coste del arrendamiento de vivienda habitual. Podrán ser beneficiarias aquellas personas "
        + "físicas cuyos ingresos no excedan de 2,5 veces el IPREM, siempre que no sean titulares de otra vivienda. La cuantía "
        + "de la ayuda ascenderá al 40 % de la renta mensual, con un máximo de 250 €. Las solicitudes deberán presentarse, "
        + "junto con la documentación acreditativa, en el plazo de quince días hábiles a contar desde el día siguiente a la "
        + "publicación del presente extracto en el BOP.";

    private static readonly GuidelineRuleSettings Settings = new(
        MaxWordsPerSentence: 15,
        MaxConnectorsPerSentence: 1,
        AllowedAcronyms: ["OK"],
        LegalJargon:
        [
            new TermSuggestion("en virtud de", "según"),
            new TermSuggestion("a contar desde", "desde"),
            new TermSuggestion("del presente", "de este"),
        ],
        DifficultWords:
        [
            new TermSuggestion("sufragar", "pagar"),
            new TermSuggestion("arrendamiento", "alquiler"),
            new TermSuggestion("cuantía", "cantidad"),
        ]);

    private readonly GuidelineAnalyzer _analyzer = new(GuidelineRules.Create(Settings));

    [Fact]
    public void Analyze_WhenAcronymIsNotExplained_FindsItWithStartAndEnd()
    {
        const string text = "La solicitud se publicará en el BOP.";

        var findings = _analyzer.Analyze(text).Findings;

        var acronym = findings.Single(finding => finding.GuidelineCode == GuidelineCodes.Acronyms);
        acronym.Fragment.ShouldBe("BOP");
        acronym.Start.ShouldBe(32);
        acronym.End.ShouldBe(35);
    }

    [Fact]
    public void Analyze_WhenTextHasAPercentage_FindsIt()
    {
        var findings = _analyzer.Analyze("La ayuda será del 40 % de la renta.").Findings;

        findings.ShouldContain(finding => finding.GuidelineCode == GuidelineCodes.Percentages && finding.Fragment == "40 %");
    }

    [Fact]
    public void Analyze_WhenSentenceHas32Words_SuggestsSplittingIt()
    {
        var sentence = "El Ayuntamiento " + string.Join(' ', Enumerable.Repeat("palabra", 30)) + ".";

        var findings = _analyzer.Analyze(sentence).Findings;

        findings.Single(finding => finding.GuidelineCode == GuidelineCodes.ShortSentences).Suggestion.ShouldContain("Divide");
    }

    [Fact]
    public void Analyze_WhenTextHasNoProblems_FindsNothing()
    {
        var analysis = _analyzer.Analyze("El Ayuntamiento da ayudas. Puede pedirlas en la web.");

        analysis.Findings.ShouldBeEmpty();
        analysis.TimedOutGuidelines.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_WithTheReferenceExample_FindsAtLeastNineProblemsOfSeveralGuidelines()
    {
        var findings = _analyzer.Analyze(ReferenceExample).Findings;

        findings.Count.ShouldBeGreaterThanOrEqualTo(9);
        findings.Select(finding => finding.GuidelineCode).Distinct().ShouldBe(
            [
                GuidelineCodes.ShortSentences,
                GuidelineCodes.OneIdeaPerSentence,
                GuidelineCodes.Acronyms,
                GuidelineCodes.Percentages,
                GuidelineCodes.NumbersInDigits,
                GuidelineCodes.PassiveVoice,
                GuidelineCodes.DifficultWords,
                GuidelineCodes.LegalJargon,
            ],
            ignoreOrder: true);
        findings.ShouldPointToFragments(ReferenceExample);
        findings.Select(finding => finding.Start).ShouldBeInOrder();
    }

    [Fact]
    public void Analyze_With20000Characters_TakesLessThan500Milliseconds()
    {
        var text = string.Concat(Enumerable.Repeat(ReferenceExample + "\n", 20_000 / ReferenceExample.Length + 1))[..20_000];
        _analyzer.Analyze(ReferenceExample);

        var stopwatch = Stopwatch.StartNew();
        var analysis = _analyzer.Analyze(text);
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500);
        analysis.Findings.ShouldNotBeEmpty();
        analysis.TimedOutGuidelines.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(PathologicalInputs))]
    public void Analyze_WithPathologicalInput_FinishesQuicklyWithoutThrowing(string text)
    {
        var stopwatch = Stopwatch.StartNew();
        var analysis = _analyzer.Analyze(text);
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2_000);
        analysis.TimedOutGuidelines.ShouldBeEmpty();
    }

    public static TheoryData<string> PathologicalInputs() => new()
    {
        new string('a', 20_000),
        string.Concat(Enumerable.Repeat("a.", 10_000)),
        string.Concat(Enumerable.Repeat("1.", 10_000)),
        string.Concat(Enumerable.Repeat("1/", 10_000)),
        string.Concat(Enumerable.Repeat("no ", 6_000)),
        string.Concat(Enumerable.Repeat("es ", 6_000)) + "x",
        string.Concat(Enumerable.Repeat("dos y ", 3_000)) + "x",
        string.Concat(Enumerable.Repeat("se ", 6_000)),
        string.Concat(Enumerable.Repeat("IVX", 6_000)),
        string.Concat(Enumerable.Repeat("en virtud ", 2_000)),
        string.Concat(Enumerable.Repeat("Sr. ", 5_000)),
        new string(' ', 20_000),
    };

    [Fact]
    public void Analyze_WhenARuleTimesOut_SkipsItAndReportsItsGuideline()
    {
        var analyzer = new GuidelineAnalyzer([new TimingOutRule(), .. GuidelineRules.Create(Settings)]);

        var analysis = analyzer.Analyze("La solicitud se publicará en el BOP.");

        analysis.TimedOutGuidelines.ShouldBe([GuidelineCodes.NumericDate]);
        analysis.Findings.ShouldContain(finding => finding.GuidelineCode == GuidelineCodes.Acronyms);
    }

    [Fact]
    public void Analyze_IsDeterministic()
    {
        _analyzer.Analyze(ReferenceExample).Findings.ShouldBe(_analyzer.Analyze(ReferenceExample).Findings);
    }

    private sealed class TimingOutRule : IGuidelineRule
    {
        public Guideline Guideline { get; } = GuidelineCatalog.Find(GuidelineCodes.NumericDate);

        public IEnumerable<Finding> Check(GuidelineText text)
        {
            yield return Finding.At(Guideline, text.Value, 0, 1, "Nunca se devuelve.");
            throw new RegexMatchTimeoutException("x", "y", TimeSpan.FromMilliseconds(1));
        }
    }
}
