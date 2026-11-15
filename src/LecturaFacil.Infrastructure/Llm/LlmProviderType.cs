// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Infrastructure.Llm;

/// <summary>Tipo de proveedor. Cualquiera puede ser principal o respaldo.</summary>
public enum LlmProviderType
{
    /// <summary>API compatible con OpenAI: Ollama, LM Studio, vLLM, llama-swap…</summary>
    OpenAICompatible,

    /// <summary>Azure AI Foundry.</summary>
    AzureFoundry,
}
