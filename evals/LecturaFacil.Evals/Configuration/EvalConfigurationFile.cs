// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Evals.Configuration;

/// <summary>
/// Forma de <c>evals/modelos.json</c>. No lleva secretos ni direcciones: el endpoint y la clave de cada proveedor se
/// leen de las variables de entorno que nombra (<see cref="ProviderEntry.EndpointVariable"/> y
/// <see cref="ProviderEntry.ApiKeyVariable"/>), igual que en la API.
/// </summary>
internal sealed class EvalConfigurationFile
{
    public string Currency { get; set; } = "USD";

    public EmbeddingEntry? Embeddings { get; set; }

    public Dictionary<string, ModelEntry> Models { get; set; } = [];
}

internal class ProviderEntry
{
    public LlmProviderType? Type { get; set; }

    public string EndpointVariable { get; set; } = string.Empty;

    public string ApiKeyVariable { get; set; } = string.Empty;
}

internal sealed class EmbeddingEntry : ProviderEntry
{
    public string Model { get; set; } = string.Empty;
}

internal sealed class ModelEntry : ProviderEntry
{
    public string DisplayName { get; set; } = string.Empty;

    public string AdapterModel { get; set; } = string.Empty;

    public string VerifierModel { get; set; } = string.Empty;

    public double Temperature { get; set; }

    public decimal InputPricePerMillion { get; set; }

    public decimal OutputPricePerMillion { get; set; }

    /// <summary>Moneda de los precios de este perfil; si falta, la del fichero.</summary>
    public string? Currency { get; set; }
}
