// SPDX-License-Identifier: Apache-2.0
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Proveedor sobre la API de chat compatible con OpenAI, mediante <see cref="IChatClient"/>. Las subclases solo
/// deciden la URL y la autenticación. Sin herramientas, con salida JSON con esquema, tokens y tiempo acotados.
/// Nunca registra el contenido de la petición ni de la respuesta, ni mensajes de excepción (podrían contenerlo).
/// </summary>
public abstract partial class ChatClientLlmProvider : ILlmProvider
{
    private readonly LlmProviderOptions _options;
    private readonly OpenAIClient _client;
    private readonly LimitsOptions _limits;
    private readonly ILogger _logger;

    protected ChatClientLlmProvider(LlmProviderOptions options, OpenAIClient client, LimitsOptions limits, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _client = client;
        _limits = limits;
        _logger = logger;
    }

    public string Name => _options.DisplayName;

    private TimeSpan Timeout => TimeSpan.FromSeconds(_limits.LlmTimeoutSeconds);

    public async Task<Result<LlmResponse<T>>> CompleteStructuredAsync<T>(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = ModelFor(request.Role);
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        ChatResponse response;
        try
        {
            using var chat = _client.GetChatClient(model).AsIChatClient();
            response = await chat.GetResponseAsync(Messages(request), ChatOptionsFor<T>(request), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(Name, request.Role, model, "timeout", null, stopwatch.ElapsedMilliseconds);
            return Result<LlmResponse<T>>.Failure(LlmErrors.ProviderUnavailable);
        }
        catch (ClientResultException exception)
        {
            LogUnavailable(Name, request.Role, model, nameof(ClientResultException), exception.Status, stopwatch.ElapsedMilliseconds);
            return Result<LlmResponse<T>>.Failure(LlmErrors.ProviderUnavailable);
        }
        catch (HttpRequestException exception)
        {
            LogUnavailable(Name, request.Role, model, nameof(HttpRequestException), (int?)exception.StatusCode, stopwatch.ElapsedMilliseconds);
            return Result<LlmResponse<T>>.Failure(LlmErrors.ProviderUnavailable);
        }

        return ToResult<T>(request.Role, model, response, stopwatch.Elapsed);
    }

    /// <summary>Opciones del cliente: sin reintentos (el respaldo es US-09) y con el tiempo máximo de los límites.</summary>
    protected static OpenAIClientOptions ClientOptions(Uri endpoint, HttpClient httpClient, LimitsOptions limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return new OpenAIClientOptions
        {
            Endpoint = endpoint,
            Transport = new HttpClientPipelineTransport(httpClient),
            NetworkTimeout = TimeSpan.FromSeconds(limits.LlmTimeoutSeconds),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };
    }

    private Result<LlmResponse<T>> ToResult<T>(LlmRole role, string model, ChatResponse response, TimeSpan duration)
    {
        var inputTokens = response.Usage?.InputTokenCount ?? 0;
        var outputTokens = response.Usage?.OutputTokenCount ?? 0;
        var validation = JsonSchemaValidator.Validate<T>(response.Text);
        if (validation.IsFailure)
        {
            LogInvalidOutput(Name, role, model, outputTokens, (long)duration.TotalMilliseconds);
            return Result<LlmResponse<T>>.Failure(LlmErrors.InvalidProviderOutput);
        }

        LogCompleted(Name, role, model, inputTokens, outputTokens, (long)duration.TotalMilliseconds);
        return Result<LlmResponse<T>>.Success(
            new LlmResponse<T>(validation.Value, Name, model, inputTokens, outputTokens, duration));
    }

    private string ModelFor(LlmRole role) => role switch
    {
        LlmRole.Adapter => _options.AdapterModel,
        LlmRole.Verifier => _options.VerifierModel,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    private static ChatMessage[] Messages(LlmRequest request) =>
    [
        new(ChatRole.System, request.SystemPrompt),
        new(ChatRole.User, request.UserMessage),
    ];

    private ChatOptions ChatOptionsFor<T>(LlmRequest request) => new()
    {
        MaxOutputTokens = Math.Min(request.MaxOutputTokens, _limits.MaxOutputTokens),
        Temperature = (float)_options.Temperature,
        ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonSchemaValidator.SchemaFor<T>(), typeof(T).Name),
        Tools = null,
    };

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "LLM call completed. Provider: {Provider}; role: {Role}; model: {Model}; input tokens: {InputTokens}; output tokens: {OutputTokens}; duration: {DurationMs} ms")]
    private partial void LogCompleted(string provider, LlmRole role, string model, long inputTokens, long outputTokens, long durationMs);


    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "LLM output does not match its schema. Provider: {Provider}; role: {Role}; model: {Model}; output tokens: {OutputTokens}; duration: {DurationMs} ms")]
    private partial void LogInvalidOutput(string provider, LlmRole role, string model, long outputTokens, long durationMs);


    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "LLM provider unavailable. Provider: {Provider}; role: {Role}; model: {Model}; reason: {Reason}; status: {Status}; duration: {DurationMs} ms")]
    private partial void LogUnavailable(string provider, LlmRole role, string model, string reason, int? status, long durationMs);

}
