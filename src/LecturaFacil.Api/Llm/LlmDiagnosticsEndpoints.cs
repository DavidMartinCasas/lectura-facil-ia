// SPDX-License-Identifier: Apache-2.0
using System.Threading.RateLimiting;
using LecturaFacil.Infrastructure.Llm;
using Microsoft.AspNetCore.RateLimiting;

namespace LecturaFacil.Api.Llm;

internal static class LlmDiagnosticsEndpoints
{
    public const string PingPath = "/dev/llm/ping";
    public const string RateLimitPolicy = "llm-diagnostics";

    // Diagnóstico de desarrollo: unas pocas comprobaciones por minuto bastan y acotan el consumo de cuota.
    public const int PingsPerWindow = 5;
    public static readonly TimeSpan PingWindow = TimeSpan.FromMinutes(1);

    /// <summary>Límite de peticiones del diagnóstico: cada ping llama a cada proveedor y rol configurados.</summary>
    public static RateLimiterOptions AddLlmDiagnosticsRateLimit(this RateLimiterOptions options) =>
        options.AddFixedWindowLimiter(RateLimitPolicy, limiter =>
        {
            limiter.PermitLimit = PingsPerWindow;
            limiter.Window = PingWindow;
            limiter.QueueLimit = 0;
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

    /// <summary>
    /// Diagnóstico de los proveedores de LLM. Solo existe en Development, solo responde a peticiones desde la propia
    /// máquina, con sesión y con límite de peticiones. En cualquier otro caso, 404 (o 401 sin sesión).
    /// </summary>
    public static IEndpointRouteBuilder MapLlmDiagnosticsEndpoints(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return endpoints;
        }

        endpoints.MapGet(PingPath, async (LlmDiagnostics diagnostics, CancellationToken cancellationToken) =>
                TypedResults.Ok(await diagnostics.PingAllAsync(cancellationToken)))
            .AddEndpointFilter(async (context, next) =>
                IsLocal(context.HttpContext) ? await next(context) : TypedResults.NotFound())
            .RequireRateLimiting(RateLimitPolicy)
            .RequireAuthorization()
            .WithName("PingLlmProviders")
            .WithSummary("Solo en desarrollo y desde la propia máquina: llamada mínima a cada proveedor de LLM y rol.")
            .ExcludeFromDescription();
        return endpoints;
    }

    private static bool IsLocal(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address && System.Net.IPAddress.IsLoopback(address);
}
