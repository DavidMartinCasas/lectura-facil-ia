// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Evals.Configuration;
using LecturaFacil.Evals.Dataset;
using LecturaFacil.Evals.DryRun;
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Evals.Reporting;
using LecturaFacil.Evals.Running;
using LecturaFacil.Evals.Similarity;
using LecturaFacil.Evals.Traps;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Testing;

namespace LecturaFacil.Evals.Tests.Running;

public sealed class ProfileEvaluatorTests
{
    private static readonly EvalText Text = new(
        "plazo",
        "Las solicitudes deberán presentarse en el plazo de quince días hábiles a contar desde la publicación del extracto.",
        "Tiene 15 días para pedir la ayuda.\nNo cuentan los sábados ni los domingos.",
        "Texto inventado",
        "CC0-1.0");

    private static readonly ModelProfile Profile = new(
        "simulado",
        new LlmProviderOptions { Type = LlmProviderType.OpenAICompatible, DisplayName = "Simulación", AdapterModel = "a", VerifierModel = "v" },
        ModelPricing.Free);

    [Fact]
    public async Task Evaluate_WhenEmbeddingsFail_LeavesSimilarityUnmeasuredAndKeepsGoing()
    {
        var evaluation = await Evaluate(new FakeLlmProvider("falso", DryRunResponder.Respond), new FailingEmbeddings());

        var text = evaluation.Texts.Single();
        text.IsSuccess.ShouldBeTrue();
        text.Similarity.ShouldBeNull();
        text.GuidelineCompliance.ShouldNotBeNull();
    }

    [Fact]
    public async Task Evaluate_WhenTheProviderIsDown_RecordsTheErrorAndTheTrapAsNotVerified()
    {
        var evaluation = await Evaluate(FakeLlmProvider.Unavailable(), new LexicalEmbeddingProvider());

        evaluation.Texts.Single().ErrorCode.ShouldBe(LlmErrors.ProviderUnavailable.Code);
        evaluation.Traps.Single().Outcome.ShouldBe(TrapOutcome.NotVerified);
    }

    [Fact]
    public async Task Evaluate_WhenTheTrapRemovesTheOnlyOriginalFigure_IsDetectedByTheCodeCheck()
    {
        var evaluation = await Evaluate(new FakeLlmProvider("falso", DryRunResponder.Respond), new LexicalEmbeddingProvider());

        evaluation.Traps.Single().Outcome.ShouldBe(TrapOutcome.DetectedByCode);
    }

    private static async Task<ModelEvaluation> Evaluate(ILlmProvider llm, IEmbeddingProvider embeddings)
    {
        await using var services = EvaluationServices.Build(new LimitsOptions(), _ => llm);
        var evaluator = new ProfileEvaluator(Profile, services, embeddings, TimeProvider.System);
        return await evaluator.EvaluateAsync([Text], [TrapGenerator.Create(Text)!], TestContext.Current.CancellationToken);
    }

    private sealed class FailingEmbeddings : IEmbeddingProvider
    {
        public string Model => "caído";

        public Task<Result<IReadOnlyList<float>>> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<float>>.Failure(LlmErrors.ProviderUnavailable));
    }
}
