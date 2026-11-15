// SPDX-License-Identifier: Apache-2.0
using System.ClientModel;
using LecturaFacil.Application.Common;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Azure AI Foundry mediante su API v1 compatible con OpenAI (<c>{recurso}/openai/v1/</c>, sin <c>api-version</c>).
/// El "modelo" de cada rol es el <b>nombre del despliegue</b>. La clave va solo en una cabecera, como en cualquier
/// cliente de OpenAI apuntado a la API v1 de Azure.
/// </summary>
public sealed class AzureFoundryProvider(
    LlmProviderOptions options,
    HttpClient httpClient,
    LimitsOptions limits,
    ILogger logger)
    : ChatClientLlmProvider(options, CreateClient(options, httpClient, limits), limits, logger)
{
    private const string V1Path = "openai/v1";

    /// <summary>
    /// Admite la URL del recurso (<c>https://mi-recurso.openai.azure.com/</c> o
    /// <c>https://mi-recurso.services.ai.azure.com/</c>) o la URL completa de la API v1.
    /// </summary>
    public static Uri V1Endpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        var trimmed = endpoint.TrimEnd('/');
        return trimmed.EndsWith($"/{V1Path}", StringComparison.OrdinalIgnoreCase)
            ? new Uri(trimmed)
            : new Uri($"{trimmed}/{V1Path}");
    }

    private static OpenAIClient CreateClient(LlmProviderOptions options, HttpClient httpClient, LimitsOptions limits)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new OpenAIClient(
            new ApiKeyCredential(options.ApiKey),
            ClientOptions(V1Endpoint(options.Endpoint), httpClient, limits));
    }
}
