// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Configuration;
using LecturaFacil.Evals.Traps;

namespace LecturaFacil.Evals.Reporting;

/// <summary>Lo medido en un texto con un modelo. Si la adaptación falla, solo tiene <see cref="ErrorCode"/>, tiempo y coste.</summary>
public sealed record TextResult(
    string TextId,
    string? ErrorCode,
    double? GuidelineCompliance,
    double? InfleszOriginal,
    double? InfleszAdapted,
    double? KeyFactRetention,
    int KeyFactsToReview,
    int KeyFactsOmitted,
    double? Similarity,
    TimeSpan Duration,
    long InputTokens,
    long OutputTokens,
    TimeSpan LlmDuration,
    decimal Cost,
    string? ReportedModel = null,
    string? PromptVersion = null)
{
    public bool IsSuccess => ErrorCode is null;

    public double? InfleszDelta => InfleszOriginal is { } original && InfleszAdapted is { } adapted ? adapted - original : null;
}

/// <summary>Lo que ha pasado con un texto trampa: si la verificación avisó del cambio introducido, y quién.</summary>
public sealed record TrapResult(string TextId, decimal OriginalFigure, decimal AlteredFigure, TrapOutcome Outcome, long InputTokens, long OutputTokens, decimal Cost)
{
    public bool Detected => Outcome is TrapOutcome.DetectedByCode or TrapOutcome.DetectedByVerifier;
}

/// <summary>Todo lo medido con un perfil.</summary>
public sealed record ModelEvaluation(ModelProfile Profile, IReadOnlyList<TextResult> Texts, IReadOnlyList<TrapResult> Traps);
