// SPDX-License-Identifier: Apache-2.0
using System.Text.Json.Serialization;

namespace LecturaFacil.Evals.Dataset;

/// <summary>
/// Un texto del dataset (<c>evals/datasets/*.json</c>): el original, su adaptación hecha por profesionales y de dónde
/// sale. Solo textos públicos o sin datos personales; cada uno declara su fuente y su licencia.
/// </summary>
public sealed record EvalText(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("original")] string Original,
    [property: JsonPropertyName("adaptacion_profesional")] string ProfessionalAdaptation,
    [property: JsonPropertyName("fuente")] string Source,
    [property: JsonPropertyName("licencia")] string License);
