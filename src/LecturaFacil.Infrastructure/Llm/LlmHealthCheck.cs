// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Salud del proveedor principal en <c>/health/llm</c>, según la última comprobación. Se registra como "degradado" y no
/// como "no disponible": la API sigue funcionando (con el respaldo o dejando los documentos en cola).
/// Sin descripción: nada de URLs, modelos ni mensajes internos.
/// </summary>
public sealed class LlmHealthCheck(ILlmAvailability availability) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(availability.Current.PrimaryAvailable
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus));
    }
}
