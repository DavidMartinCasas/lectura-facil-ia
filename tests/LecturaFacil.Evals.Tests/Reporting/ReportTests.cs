// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Evals.Configuration;
using LecturaFacil.Evals.Dataset;
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Evals.Reporting;
using LecturaFacil.Evals.Traps;
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Evals.Tests.Reporting;

public sealed class ReportTests
{
    private static readonly ModelProfile Profile = new(
        "gemma",
        new LlmProviderOptions
        {
            Type = LlmProviderType.OpenAICompatible,
            DisplayName = "Servidor | propio",
            ApiKey = "clave-secreta",
            Endpoint = "http://localhost:8080/v1",
            AdapterModel = "adaptador",
            VerifierModel = "verificador",
        },
        new ModelPricing(1m, 2m));

    private static readonly ModelEvaluation Evaluation = new(
        Profile,
        [
            Text("a", guidelines: 1.0, retention: 0.5, similarity: 0.8, cost: 0.5m),
            Text("b", guidelines: 0.5, retention: null, similarity: 0.6, cost: 0.25m),
            new TextResult("c", "llm.provider_unavailable", null, null, null, null, 0, 0, null, TimeSpan.FromSeconds(4), 10, 0, TimeSpan.Zero, 0.01m),
        ],
        [
            new TrapResult("a", 15, 16, TrapOutcome.DetectedByVerifier, 100, 10, 0.1m),
            new TrapResult("b", 3, 4, TrapOutcome.Missed, 100, 10, 0.1m),
            new TrapResult("c", 7, 8, TrapOutcome.DetectedByCode, 100, 10, 0.1m),
            new TrapResult("d", 9, 10, TrapOutcome.NotVerified, 100, 10, 0.1m),
        ]);

    [Fact]
    public void Summary_WhenSomeTextsFail_AveragesOnlyTheAdaptedOnesButCountsAllTheCost()
    {
        var summary = ModelSummary.From(Evaluation);

        summary.SucceededTexts.ShouldBe(2);
        summary.TotalTexts.ShouldBe(3);
        summary.GuidelineCompliance.ShouldBe(0.75);
        summary.KeyFactRetention.ShouldBe(0.5);
        summary.Similarity!.Value.ShouldBe(0.7, tolerance: 1e-9);
        summary.InfleszDelta.ShouldBe(20);
        summary.KeyFactsToReview.ShouldBe(2);
        summary.KeyFactsOmitted.ShouldBe(0);
        summary.Cost.ShouldBe(1.16m);
        summary.TrapsVerified.ShouldBe(3);
        summary.TrapsNotVerified.ShouldBe(1);
        summary.Recall!.Value.ShouldBe(2.0 / 3, tolerance: 1e-9);
        summary.VerifierRecall!.Value.ShouldBe(1.0 / 3, tolerance: 1e-9);
        summary.TokensPerSecond.ShouldBe(10);
        summary.MeanDuration.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Markdown_Always_EscapesCellsAndNeverIncludesTheKeyOrEndpoint()
    {
        var report = MarkdownReport.Render(Run(dryRun: false), [Evaluation]);

        report.ShouldContain("Servidor \\| propio");
        report.ShouldNotContain("clave-secreta");
        report.ShouldNotContain("localhost:8080");
        report.ShouldNotContain("Simulación (`--dry-run`).");
        report.ShouldContain("| gemma | 2 de 3 | 75,0 % |");
        report.ShouldContain("| gemma | b | 3 → 4 | **No** |");
        report.ShouldContain("| gemma | a | 15 → 16 | Sí (verificador) |");
        report.ShouldContain("| gemma | c | 7 → 8 | Sí (solo código) |");
        report.ShouldContain("| gemma | d | 9 → 10 | Sin verificar |");
        report.ShouldContain("66,7 % (2 de 3) | 33,3 % (1 de 3); 1 sin verificar |");
        report.ShouldContain("| gemma | c | llm.provider\\_unavailable |");
    }

    [Fact]
    public void Markdown_WhenDatasetSourceHasMarkup_NeutralizesIt()
    {
        var run = Run(dryRun: true) with
        {
            Texts = [new EvalText("a", "x", "y", "![imagen](https://ejemplo.invalid/x.png) <b>negrita</b>", "CC0-1.0")],
        };

        var report = MarkdownReport.Render(run, []);

        report.ShouldContain("\\!\\[imagen\\](https://ejemplo.invalid/x.png) \\<b\\>negrita\\</b\\>");
        report.ShouldContain("Simulación (`--dry-run`).");
    }

    [Fact]
    public void Csv_WhenRendered_HasOneRowPerTextAndTrapWithInvariantNumbers()
    {
        var lines = CsvReport.Render([Evaluation]).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines[0].ShouldBe(CsvReport.Header);
        lines.Length.ShouldBe(1 + 3 + 4);
        lines[1].ShouldStartWith("\"gemma\",text,\"a\",\"ok\",1,50,70,20,0.5,1,0,0.8,1000,1000,100,10000,0.5,,,,");
        lines[3].ShouldContain("\"llm.provider_unavailable\"");
        lines[4].ShouldEndWith(",15,16,true,verifier");
        lines[5].ShouldEndWith(",3,4,false,missed");
        lines[6].ShouldEndWith(",7,8,true,code");
        lines[7].ShouldEndWith(",9,10,false,not_verified");
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1", "\"'+1\"")]
    [InlineData("@suma", "\"'@suma\"")]
    public void Csv_WhenATextLooksLikeAFormula_PrefixesIt(string model, string expected)
    {
        var evaluation = Evaluation with { Profile = Profile with { Name = model }, Traps = [] };

        CsvReport.Render([evaluation]).Split('\n')[1].ShouldStartWith(expected + ",text,");
    }

    private static TextResult Text(string id, double guidelines, double? retention, double similarity, decimal cost) =>
        new(id, null, guidelines, 50, 70, retention, 1, 0, similarity, TimeSpan.FromSeconds(1), 1000, 100, TimeSpan.FromSeconds(10), cost, "modelo", "adapter.v1");

    private static RunInfo Run(bool dryRun) =>
        new(
            new DateTimeOffset(2026, 11, 24, 9, 0, 0, TimeSpan.Zero),
            dryRun,
            "USD",
            "bge-m3",
            [new PromptInfo("adapter.v1", "abc")],
            new LimitsOptions(),
            [new EvalText("a", "x", "y", "Fuente", "CC0-1.0")]);
}
