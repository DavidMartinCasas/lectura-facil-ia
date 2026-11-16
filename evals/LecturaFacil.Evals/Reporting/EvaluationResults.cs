// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Configuration;

namespace LecturaFacil.Evals.Reporting;

/// <summary>Lo medido en un texto con un modelo. Si la adaptación falla, solo tiene <see cref="ErrorCode"/>, tiempo y coste.</summary>
public sealed record TextResult(
    string TextId,
    string? ErrorCode,
    double? GuidelineCompliance,
    double? InfleszOriginal,
    double? InfleszAdapted,
    double? KeyFactRetention,
    int KeyFactWarnings,
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

/// <summary>Lo que ha pasado con un texto trampa: si la verificación avisó del cambio introducido.</summary>
public sealed record TrapResult(string TextId, decimal OriginalFigure, decimal AlteredFigure, bool Detected, long InputTokens, long OutputTokens, decimal Cost);

/// <summary>Todo lo medido con un perfil.</summary>
public sealed record ModelEvaluation(ModelProfile Profile, IReadOnlyList<TextResult> Texts, IReadOnlyList<TrapResult> Traps);
