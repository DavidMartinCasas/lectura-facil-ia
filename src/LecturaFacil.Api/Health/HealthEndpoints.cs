// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LecturaFacil.Api.Health;

internal static class HealthEndpoints
{
    public const string Path = "/health";
    public const string LlmPath = "/health/llm";

    /// <summary>
    /// <c>/health</c>: la API y su base de datos (lo que usan Docker y la prueba de humo). Que el modelo no responda no
    /// hace que la API esté caída: eso se ve en <c>/health/llm</c> (<c>Degraded</c>, HTTP 200) y en <c>/api/status/llm</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(Path, new HealthCheckOptions
        {
            Predicate = registration => !IsLlm(registration),
            ResponseWriter = WriteStatusOnlyAsync,
        });
        endpoints.MapHealthChecks(LlmPath, new HealthCheckOptions
        {
            Predicate = IsLlm,
            ResponseWriter = WriteStatusOnlyAsync,
        });
        return endpoints;
    }

    private static bool IsLlm(HealthCheckRegistration registration) =>
        registration.Tags.Contains(DependencyInjection.LlmHealthCheckTag);

    /// <summary>Solo el estado global: ni nombres de comprobaciones ni excepciones, que darían pistas internas.</summary>
    private static Task WriteStatusOnlyAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(
            new HealthResponse(report.Status.ToString()),
            context.RequestAborted);

    private sealed record HealthResponse(string Status);
}
