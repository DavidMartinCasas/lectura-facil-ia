// SPDX-License-Identifier: Apache-2.0
using System.Threading.RateLimiting;
using LecturaFacil.Infrastructure.Observability;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Observability;

/// <summary>
/// Lo que necesita el Sentry del frontend (US-17): su configuración, que se lee al cargar la página (así el DSN se
/// inyecta en el despliegue, no al compilar), y el reenvío de sus informes a Sentry por el mismo origen.
/// </summary>
internal static class MonitoringEndpoints
{
    public const string ConfigPath = "/api/monitoring/config";
    public const string TunnelPath = "/api/monitoring/sentry";
    public const string TunnelRateLimitPolicy = "monitoring-tunnel";

    public static IEndpointRouteBuilder MapMonitoringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ConfigPath, (IOptions<ObservabilityOptions> options, AppVersion version) =>
                TypedResults.Ok(MonitoringConfigResponse.From(options.Value, version)))
            .WithName("GetMonitoringConfig")
            .WithSummary("Configuración pública del informe de errores del navegador.");

        endpoints.MapPost(TunnelPath, ForwardAsync)
            .RequireRateLimiting(TunnelRateLimitPolicy)
            .ExcludeFromDescription();
        return endpoints;
    }

    /// <summary>Límite global de informes por minuto (<see cref="ObservabilityOptions.FrontendReportsPerMinute"/>).</summary>
    public static RateLimiterOptions AddMonitoringTunnelRateLimit(this RateLimiterOptions options) =>
        options.AddPolicy(TunnelRateLimitPolicy, context =>
        {
            var observability = context.RequestServices.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
            return RateLimitPartition.GetFixedWindowLimiter(TunnelRateLimitPolicy, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = observability.FrontendReportsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
        });

    private static async Task<IResult> ForwardAsync(
        HttpRequest request,
        SentryEnvelopeForwarder forwarder,
        IOptions<ObservabilityOptions> options,
        CancellationToken cancellationToken)
    {
        if (!forwarder.IsEnabled)
        {
            return TypedResults.NotFound();
        }

        var envelope = await ReadBodyAsync(request, options.Value.MaxFrontendReportBytes, cancellationToken);
        if (envelope is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "El informe de errores es demasiado grande.");
        }

        var result = await forwarder.ForwardAsync(envelope.Value, cancellationToken);
        if (result.IsSuccess)
        {
            return TypedResults.Ok();
        }

        return result.Error == SentryEnvelopeForwarder.InvalidEnvelope
            ? TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error.Message)
            : TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: result.Error!.Message);
    }

    /// <summary>El cuerpo, o <c>null</c> si pasa de <paramref name="maxBytes"/>. Se corta al leer, sin fiarse de Content-Length.</summary>
    private static async Task<ReadOnlyMemory<byte>?> ReadBodyAsync(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        if (request.ContentLength > maxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}

/// <param name="Dsn"><c>null</c> si el informe de errores del navegador no está configurado.</param>
internal sealed record MonitoringConfigResponse(string? Dsn, string Environment, string Release)
{
    public static MonitoringConfigResponse From(ObservabilityOptions options, AppVersion version) => new(
        string.IsNullOrEmpty(options.FrontendSentryDsn) ? null : options.FrontendSentryDsn,
        options.Environment,
        version.Value);
}
