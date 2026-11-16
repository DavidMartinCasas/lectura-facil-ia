// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Evals.Running;

/// <summary>
/// Decorador que suma los tokens y el tiempo de respuesta de todas las llamadas válidas (adaptador, reintentos y
/// verificador), para el coste y la velocidad de cada texto. No guarda ningún texto.
/// </summary>
public sealed class MeteredLlmProvider(ILlmProvider inner) : ILlmProvider
{
    private long _inputTokens;
    private long _outputTokens;
    private long _durationTicks;

    public string Name => inner.Name;

    public LlmUsage Usage => new(
        Interlocked.Read(ref _inputTokens),
        Interlocked.Read(ref _outputTokens),
        TimeSpan.FromTicks(Interlocked.Read(ref _durationTicks)));

    /// <summary>Lo consumido desde la última vez, y vuelve a empezar a contar.</summary>
    public LlmUsage TakeUsage() => new(
        Interlocked.Exchange(ref _inputTokens, 0),
        Interlocked.Exchange(ref _outputTokens, 0),
        TimeSpan.FromTicks(Interlocked.Exchange(ref _durationTicks, 0)));

    public async Task<Result<LlmResponse<T>>> CompleteStructuredAsync<T>(LlmRequest request, CancellationToken cancellationToken)
    {
        var response = await inner.CompleteStructuredAsync<T>(request, cancellationToken);
        if (response.IsSuccess)
        {
            Interlocked.Add(ref _inputTokens, response.Value.InputTokens);
            Interlocked.Add(ref _outputTokens, response.Value.OutputTokens);
            Interlocked.Add(ref _durationTicks, response.Value.Duration.Ticks);
        }

        return response;
    }
}

/// <summary>Tokens y tiempo de respuesta de los modelos.</summary>
public sealed record LlmUsage(long InputTokens, long OutputTokens, TimeSpan Duration);
