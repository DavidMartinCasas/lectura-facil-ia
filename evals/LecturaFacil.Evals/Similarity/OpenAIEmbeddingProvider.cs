// SPDX-License-Identifier: Apache-2.0
using System.ClientModel;
using System.ClientModel.Primitives;
using LecturaFacil.Evals.Configuration;
using LecturaFacil.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using OpenAI;

namespace LecturaFacil.Evals.Similarity;

/// <summary>
/// Embeddings por la API de OpenAI: cualquier servidor compatible (BGE-M3 con llama-swap, Ollama…) o Azure AI Foundry
/// por su API v1 (el modelo es el nombre del despliegue). Sin reintentos y con tiempo máximo, como los proveedores de
/// chat, y sin seguir redirecciones. El texto se trunca a <see cref="MaxCharacters"/> para acotar la petición.
/// </summary>
public sealed class OpenAIEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    /// <summary>Unos 8 000 tokens, el contexto de BGE-M3 y de text-embedding-3-small.</summary>
    public const int MaxCharacters = 24_000;

    private readonly HttpClient _httpClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public OpenAIEmbeddingProvider(EmbeddingSettings settings, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _httpClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = timeout };
        var endpoint = settings.Type == LlmProviderType.AzureFoundry
            ? AzureFoundryProvider.V1Endpoint(settings.Endpoint)
            : new Uri(settings.Endpoint.TrimEnd('/'));
        var options = new OpenAIClientOptions
        {
            Endpoint = endpoint,
            Transport = new HttpClientPipelineTransport(_httpClient),
            NetworkTimeout = timeout,
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
        Model = settings.Model;
        _generator = new OpenAIClient(new ApiKeyCredential(settings.ApiKey), options)
            .GetEmbeddingClient(settings.Model)
            .AsIEmbeddingGenerator();
    }

    public string Model { get; }

    public async Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        var input = text.Length > MaxCharacters ? text[..MaxCharacters] : text;
        var embedding = await _generator.GenerateAsync(input, cancellationToken: cancellationToken);
        return embedding.Vector.ToArray();
    }

    public void Dispose()
    {
        _generator.Dispose();
        _httpClient.Dispose();
    }
}
