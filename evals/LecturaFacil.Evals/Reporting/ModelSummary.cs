// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Evals.Traps;

namespace LecturaFacil.Evals.Reporting;

/// <summary>Las métricas de un modelo sobre todo el dataset (una fila de la tabla de resultados).</summary>
public sealed record ModelSummary(
    string Name,
    int SucceededTexts,
    int TotalTexts,
    double? GuidelineCompliance,
    double? InfleszDelta,
    double? KeyFactRetention,
    int KeyFactsToReview,
    int KeyFactsOmitted,
    double? Similarity,
    TimeSpan? MeanDuration,
    double? TokensPerSecond,
    decimal Cost,
    int TrapsDetected,
    int TrapsDetectedByVerifier,
    int TrapsVerified,
    int TrapsNotVerified)
{
    /// <summary>Trampas avisadas (por el verificador o por la comprobación de cifras) / trampas verificadas.</summary>
    public double? Recall => EvalMetrics.Recall(TrapsDetected, TrapsVerified);

    /// <summary>Trampas en las que el verificador listó el dato cambiado / trampas verificadas: lo que aporta el modelo.</summary>
    public double? VerifierRecall => EvalMetrics.Recall(TrapsDetectedByVerifier, TrapsVerified);

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
            succeeded.Sum(text => text.KeyFactsToReview),
            succeeded.Sum(text => text.KeyFactsOmitted),
            EvalMetrics.Mean(succeeded.Select(text => text.Similarity)),
            texts.Count == 0 ? null : TimeSpan.FromTicks((long)texts.Average(text => text.Duration.Ticks)),
            EvalMetrics.TokensPerSecond(outputTokens, llmDuration),
            texts.Sum(text => text.Cost) + evaluation.Traps.Sum(trap => trap.Cost),
            evaluation.Traps.Count(trap => trap.Detected),
            evaluation.Traps.Count(trap => trap.Outcome == TrapOutcome.DetectedByVerifier),
            evaluation.Traps.Count(trap => trap.Outcome != TrapOutcome.NotVerified),
            evaluation.Traps.Count(trap => trap.Outcome == TrapOutcome.NotVerified));
    }
}
