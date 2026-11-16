// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Dataset;

namespace LecturaFacil.Evals.Traps;

/// <summary>
/// Texto trampa: la adaptación profesional de <see cref="Source"/> con una cifra cambiada a propósito. El verificador
/// debería avisar de ese cambio.
/// </summary>
public sealed record Trap(EvalText Source, string AlteredAdaptation, decimal OriginalFigure, decimal AlteredFigure);
