// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Metrics;

namespace LecturaFacil.Evals.Reporting;

/// <summary>Las métricas de un modelo sobre todo el dataset (una fila de la tabla de resultados).</summary>
public sealed record ModelSummary(
    string Name,
    int SucceededTexts,
    int TotalTexts,
    double? GuidelineCompliance,
    double? InfleszDelta,
    double? KeyFactRetention,
    int KeyFactWarnings,
    double? Similarity,
    TimeSpan? MeanDuration,
    double? TokensPerSecond,
    decimal Cost,
    int TrapsDetected,
    int TrapsIntroduced)
{
    public double? VerifierRecall => EvalMetrics.Recall(TrapsDetected, TrapsIntroduced);

    /// <summary>Medias de los textos adaptados; el coste y el tiempo cuentan también los que fallan y las trampas.</summary>
    public static ModelSummary From(ModelEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);

        var texts = evaluation.Texts;
        var succeeded = texts.Where(text => text.IsSuccess).ToList();
        var outputTokens = texts.Sum(text => text.OutputTokens);
        var llmDuration = texts.Aggregate(TimeSpan.Zero, (total, text) => total + text.LlmDuration);
        return new ModelSummary(
            evaluation.Profile.Name,
            succeeded.Count,
            texts.Count,
            EvalMetrics.Mean(succeeded.Select(text => text.GuidelineCompliance)),
            EvalMetrics.Mean(succeeded.Select(text => text.InfleszDelta)),
            EvalMetrics.Mean(succeeded.Select(text => text.KeyFactRetention)),
            succeeded.Sum(text => text.KeyFactWarnings),
            EvalMetrics.Mean(succeeded.Select(text => text.Similarity)),
            texts.Count == 0 ? null : TimeSpan.FromTicks((long)texts.Average(text => text.Duration.Ticks)),
            EvalMetrics.TokensPerSecond(outputTokens, llmDuration),
            texts.Sum(text => text.Cost) + evaluation.Traps.Sum(trap => trap.Cost),
            evaluation.Traps.Count(trap => trap.Detected),
            evaluation.Traps.Count);
    }
}
