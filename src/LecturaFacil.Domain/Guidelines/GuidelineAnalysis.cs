// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Domain.Guidelines;

/// <param name="Findings">Hallazgos ordenados por posición.</param>
/// <param name="TimedOutGuidelines">Pautas que no se han podido comprobar porque una búsqueda agotó su tiempo.</param>
public sealed record GuidelineAnalysis(IReadOnlyList<Finding> Findings, IReadOnlyList<string> TimedOutGuidelines);
