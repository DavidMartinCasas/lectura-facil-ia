// SPDX-License-Identifier: Apache-2.0
using System.ClientModel;
using LecturaFacil.Application.Common;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Cualquier servidor con la API de chat de OpenAI (Ollama, LM Studio, vLLM, llama-swap…). El endpoint es la URL
/// base de la API, por ejemplo <c>http://localhost:11434/v1</c>; la clave va en la cabecera <c>Authorization</c>.
/// </summary>
public sealed class OpenAICompatibleProvider(
    LlmProviderOptions options,
    HttpClient httpClient,
    LimitsOptions limits,
    ILogger logger)
    : ChatClientLlmProvider(options, CreateClient(options, httpClient, limits), limits, logger)
{
    private static OpenAIClient CreateClient(LlmProviderOptions options, HttpClient httpClient, LimitsOptions limits)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            ClientOptions(new Uri(options.Endpoint.TrimEnd('/')), httpClient, limits));
    }
}
