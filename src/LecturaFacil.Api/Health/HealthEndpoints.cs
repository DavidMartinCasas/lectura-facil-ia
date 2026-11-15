// SPDX-License-Identifier: Apache-2.0
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LecturaFacil.Api.Health;

internal static class HealthEndpoints
{
    public const string Path = "/health";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(Path, new HealthCheckOptions { ResponseWriter = WriteStatusOnlyAsync });
        return endpoints;
    }

    /// <summary>Solo el estado global: ni nombres de comprobaciones ni excepciones, que darían pistas internas.</summary>
    private static Task WriteStatusOnlyAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(
            new HealthResponse(report.Status.ToString()),
            context.RequestAborted);

    private sealed record HealthResponse(string Status);
}
