// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Evals.Similarity;

/// <summary>
/// Puerto hacia un modelo de embeddings (BGE-M3 en local o <c>text-embedding-3-small</c> en Azure), para medir la
/// similitud de una adaptación con la profesional. Solo lo usa la evaluación.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Nombre del modelo, para el informe.</summary>
    string Model { get; }

    /// <returns>El vector, o <c>LlmErrors.ProviderUnavailable</c> si el servicio falla o no responde a tiempo.</returns>
    Task<Result<IReadOnlyList<float>>> EmbedAsync(string text, CancellationToken cancellationToken);
}
