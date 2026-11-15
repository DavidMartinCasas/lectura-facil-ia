// SPDX-License-Identifier: Apache-2.0
using System.Collections.Concurrent;
using System.Text.Json;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Testing;

/// <summary>
/// Proveedor falso para los tests: devuelve respuestas grabadas (<c>tests/fixtures/llm/&lt;rol&gt;/&lt;caso&gt;.json</c>) y las
/// valida con el mismo <see cref="JsonSchemaValidator"/> que los proveedores reales. Nunca sale a la red.
/// </summary>
public sealed class FakeLlmProvider(string name, Func<LlmRequest, LlmRecording> respond) : ILlmProvider
{
    private readonly ConcurrentQueue<LlmRequest> _requests = new();

    public string Name { get; } = name;

    public IReadOnlyCollection<LlmRequest> Requests => _requests;

    /// <summary>Responde siempre con la grabación indicada, por ejemplo <c>ping/ok</c>.</summary>
    public static FakeLlmProvider FromFixture(string fixture, string name = "Proveedor falso") =>
        new(name, _ => LlmRecording.Load(fixture));

    /// <summary>Simula un proveedor caído o que no responde a tiempo.</summary>
    public static FakeLlmProvider Unavailable(string name = "Proveedor caído") =>
        new(name, _ => LlmRecording.Unavailable);

    public Task<Result<LlmResponse<T>>> CompleteStructuredAsync<T>(LlmRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requests.Enqueue(request);

        var recording = respond(request);
        if (recording.IsUnavailable)
        {
            return Task.FromResult(Result<LlmResponse<T>>.Failure(LlmErrors.ProviderUnavailable));
        }

        var validation = JsonSchemaValidator.Validate<T>(recording.Content);
        return Task.FromResult(validation.IsSuccess
            ? Result<LlmResponse<T>>.Success(new LlmResponse<T>(
                validation.Value,
                Name,
                recording.Model,
                recording.InputTokens,
                recording.OutputTokens,
                TimeSpan.FromMilliseconds(recording.DurationMs)))
            : Result<LlmResponse<T>>.Failure(LlmErrors.InvalidProviderOutput));
    }
}

/// <summary>Respuesta grabada de un modelo. <see cref="Content"/> es el texto tal cual lo devolvió.</summary>
public sealed record LlmRecording(
    string Model,
    string? Content,
    long InputTokens,
    long OutputTokens,
    long DurationMs,
    bool IsUnavailable = false)
{
    public static readonly LlmRecording Unavailable = new("ninguno", null, 0, 0, 0, IsUnavailable: true);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static LlmRecording Load(string fixture)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "llm", $"{fixture}.json");
        return JsonSerializer.Deserialize<LlmRecording>(File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException($"Empty fixture: {fixture}.");
    }
}
