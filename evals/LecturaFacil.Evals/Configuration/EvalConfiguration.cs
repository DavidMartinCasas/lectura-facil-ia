// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Evals.Metrics;
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Evals.Configuration;

/// <summary>Los perfiles elegidos con <c>--models</c>, ya validados y con sus claves leídas del entorno.</summary>
public sealed record EvalConfiguration(IReadOnlyList<ModelProfile> Models, EmbeddingSettings? Embeddings, string Currency);

/// <summary>Un perfil de evaluación: un proveedor con su modelo adaptador y su verificador, y su precio.</summary>
public sealed record ModelProfile(string Name, LlmProviderOptions Provider, ModelPricing Pricing);

/// <summary>Modelo de embeddings para la similitud con la adaptación profesional (el mismo para todos los perfiles).</summary>
public sealed record EmbeddingSettings(LlmProviderType Type, string Endpoint, [property: Secret] string ApiKey, string Model)
{
    /// <summary>Sin la clave: es lo que se puede escribir en el informe.</summary>
    public override string ToString() => $"{Type} · {Model}";
}
