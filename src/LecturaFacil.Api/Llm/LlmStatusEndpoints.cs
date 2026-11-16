// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;

namespace LecturaFacil.Api.Llm;

internal static class LlmStatusEndpoints
{
    public const string Path = "/api/status/llm";

    /// <summary>
    /// Si el principal y el respaldo (solo si está habilitado) están disponibles, según la última comprobación de salud.
    /// No llama a ningún proveedor y no expone URLs, IPs, nombres de modelos ni mensajes internos.
    /// </summary>
    public static IEndpointRouteBuilder MapLlmStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, (ILlmAvailability availability) => TypedResults.Ok(LlmStatusResponse.From(availability.Current)))
            .RequireAuthorization()
            .WithName("GetLlmStatus")
            .WithSummary("Disponibilidad del proveedor de IA principal y del de respaldo, si está habilitado.");
        return endpoints;
    }
}

/// <param name="Fallback"><c>null</c> si el respaldo no está habilitado.</param>
internal sealed record LlmStatusResponse(LlmProviderStatus Primary, LlmProviderStatus? Fallback)
{
    public static LlmStatusResponse From(LlmAvailabilityStatus status) => new(
        new LlmProviderStatus(status.PrimaryAvailable),
        status.FallbackAvailable is { } fallback ? new LlmProviderStatus(fallback) : null);
}

internal sealed record LlmProviderStatus(bool Available);
