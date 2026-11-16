// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Último estado conocido de los proveedores. Antes de la primera comprobación se suponen disponibles: así se
/// intenta siempre el principal y nunca se usa el respaldo sin motivo.
/// </summary>
public sealed class LlmAvailability(bool fallbackEnabled) : ILlmAvailability
{
    private LlmAvailabilityStatus _current = new(PrimaryAvailable: true, FallbackAvailable: fallbackEnabled ? true : null);

    public LlmAvailabilityStatus Current => Volatile.Read(ref _current);

    /// <summary>Guarda el nuevo estado y devuelve el anterior.</summary>
    public LlmAvailabilityStatus Update(LlmAvailabilityStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return Interlocked.Exchange(ref _current, status);
    }
}
