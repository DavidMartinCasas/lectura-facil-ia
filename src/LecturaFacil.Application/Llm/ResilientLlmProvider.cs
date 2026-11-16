// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;

namespace LecturaFacil.Application.Llm;

/// <summary>
/// Decorador con la política de respaldo: principal y, solo si el respaldo está habilitado, respaldo cuando el
/// principal no está disponible. Ni los casos de uso ni los adaptadores conocen esta política.
/// </summary>
/// <remarks>
/// Con el respaldo deshabilitado nunca se le llama, aunque exista: es la garantía de privacidad de los despliegues
/// solo locales. Si el principal no está disponible y no hay respaldo, devuelve
/// <see cref="LlmErrors.ProviderUnavailable"/> y quien llama decide (por ejemplo, dejar el documento en cola).
/// Una respuesta que no cumple el esquema no activa el respaldo: el principal respondió.
/// </remarks>
public sealed class ResilientLlmProvider : ILlmProvider
{
    private readonly ILlmProvider _primary;
    private readonly ILlmProvider? _fallback;
    private readonly ILlmAvailability _availability;

    public ResilientLlmProvider(
        ILlmProvider primary,
        ILlmProvider? fallback,
        bool fallbackEnabled,
        ILlmAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(availability);

        _primary = primary;
        _fallback = fallbackEnabled ? fallback : null;
        _availability = availability;
    }

    public string Name => _primary.Name;

    public async Task<Result<LlmResponse<T>>> CompleteStructuredAsync<T>(LlmRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Sin respaldo no hay alternativa: se intenta siempre el principal (el circuit breaker ya falla rápido).
        if (_fallback is null)
        {
            return await _primary.CompleteStructuredAsync<T>(request, cancellationToken);
        }

        if (_availability.Current.PrimaryAvailable)
        {
            var result = await _primary.CompleteStructuredAsync<T>(request, cancellationToken);
            if (result.Error != LlmErrors.ProviderUnavailable)
            {
                return result;
            }
        }

        var fallback = await _fallback.CompleteStructuredAsync<T>(request, cancellationToken);
        return fallback.IsSuccess
            ? Result<LlmResponse<T>>.Success(fallback.Value with { Role = LlmProviderRole.Fallback })
            : fallback;
    }
}
