// SPDX-License-Identifier: Apache-2.0
using System.ClientModel;
using System.ClientModel.Primitives;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
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
    /// <summary>Por debajo de los 8 192 tokens de contexto de BGE-M3 y de text-embedding-3-small, con margen.</summary>
    public const int MaxCharacters = 16_000;

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

    /// <summary>
    /// Un fallo del servicio (error HTTP, red o tiempo agotado) es un error esperado: la similitud de ese texto queda sin
    /// medir y la evaluación sigue. Ni el texto ni la respuesta del servicio se escriben en ningún sitio.
    /// </summary>
    public async Task<Result<IReadOnlyList<float>>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        var input = text.Length > MaxCharacters ? text[..MaxCharacters] : text;
        try
        {
            var embedding = await _generator.GenerateAsync(input, cancellationToken: cancellationToken);
            return Result<IReadOnlyList<float>>.Success(embedding.Vector.ToArray());
        }
        catch (Exception exception) when (exception is ClientResultException or HttpRequestException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return Result<IReadOnlyList<float>>.Failure(LlmErrors.ProviderUnavailable);
        }
    }

    public void Dispose()
    {
        _generator.Dispose();
        _httpClient.Dispose();
    }
}
