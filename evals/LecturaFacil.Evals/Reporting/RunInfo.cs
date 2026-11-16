// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Evals.Dataset;

namespace LecturaFacil.Evals.Reporting;

/// <summary>Lo necesario para repetir la evaluación con la misma configuración.</summary>
/// <param name="Prompts">Nombre de cada prompt y el SHA-256 de su texto.</param>
public sealed record RunInfo(
    DateTimeOffset StartedAt,
    bool DryRun,
    string Currency,
    string EmbeddingModel,
    IReadOnlyList<PromptInfo> Prompts,
    LimitsOptions Limits,
    IReadOnlyList<EvalText> Texts);

public sealed record PromptInfo(string Name, string Sha256);
