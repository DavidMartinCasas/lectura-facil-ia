// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Crea cada proveedor según su tipo configurado. Es el único sitio que distingue entre tipos de proveedor:
/// cambiar de proveedor es cambiar la configuración, no el código.
/// </summary>
public sealed class LlmProviderFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<LimitsOptions> limits,
    ILoggerFactory loggerFactory)
{
    public const string PrimaryHttpClient = "Llm.Primary";
    public const string FallbackHttpClient = "Llm.Fallback";

    public ILlmProvider Create(LlmProviderOptions options, string httpClientName)
    {
        ArgumentNullException.ThrowIfNull(options);

        var httpClient = httpClientFactory.CreateClient(httpClientName);
        var logger = loggerFactory.CreateLogger<ChatClientLlmProvider>();
        return options.Type switch
        {
            LlmProviderType.OpenAICompatible => new OpenAICompatibleProvider(options, httpClient, limits.Value, logger),
            LlmProviderType.AzureFoundry => new AzureFoundryProvider(options, httpClient, limits.Value, logger),
            _ => throw new InvalidOperationException($"Unsupported LLM provider type: {options.Type}."),
        };
    }
}
