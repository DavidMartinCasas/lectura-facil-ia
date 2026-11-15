// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Llm;

namespace LecturaFacil.Api.Llm;

internal static class LlmDiagnosticsEndpoints
{
    public const string PingPath = "/dev/llm/ping";

    /// <summary>
    /// Diagnóstico de los proveedores de LLM. Solo existe en Development: en cualquier otro entorno la ruta no se
    /// registra y responde 404.
    /// </summary>
    public static IEndpointRouteBuilder MapLlmDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return endpoints;
        }

        endpoints.MapGet(PingPath, async (LlmDiagnostics diagnostics, CancellationToken cancellationToken) =>
                TypedResults.Ok(await diagnostics.PingAllAsync(cancellationToken)))
            .WithName("PingLlmProviders")
            .WithSummary("Solo en desarrollo: llamada mínima a cada proveedor de LLM y rol configurados.")
            .ExcludeFromDescription();
        return endpoints;
    }
}
