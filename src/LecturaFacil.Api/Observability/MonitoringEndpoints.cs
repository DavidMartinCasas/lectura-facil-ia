// SPDX-License-Identifier: Apache-2.0
using System.Threading.RateLimiting;
using LecturaFacil.Api.Security;
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

    private const int ReadChunkBytes = 16 * 1024;

    public static IEndpointRouteBuilder MapMonitoringEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(ConfigPath, (IOptions<ObservabilityOptions> options, AppVersion version) =>
                TypedResults.Ok(MonitoringConfigResponse.From(options.Value, version)))
            .AllowAnonymous()
            .WithName("GetMonitoringConfig")
            .WithSummary("Configuración pública del informe de errores del navegador.");

        // Anónimo y sin antiforgery a propósito: también hay errores en la pantalla de login y el SDK de Sentry no envía
        // la cabecera X-XSRF-TOKEN. Nunca depende de la sesión; la protección es exigir el mismo origen (IsSameOrigin),
        // el DSN exacto y los límites de tamaño y de peticiones.
        endpoints.MapPost(TunnelPath, ForwardAsync)
            .AllowAnonymous()
            .RequireRateLimiting(TunnelRateLimitPolicy)
            .DisableAntiforgery()
            .ExcludeFromDescription();
        return endpoints;
    }

    /// <summary>
    /// Informes por minuto y por cliente (<see cref="ObservabilityOptions.FrontendReportsPerMinute"/>): así un cliente no
    /// deja sin informes a los demás. El cliente es la IP que pone nginx en <c>X-Forwarded-For</c> (la sustituye, no la
    /// añade) o, sin proxy, la de la conexión. El tope total de eventos se fija en el proyecto de Sentry.
    /// </summary>
    public static RateLimiterOptions AddMonitoringTunnelRateLimit(this RateLimiterOptions options) =>
        options.AddPolicy(TunnelRateLimitPolicy, context =>
        {
            var observability = context.RequestServices.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
            return RateLimitPartition.GetFixedWindowLimiter(ClientAddress.Of(context), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = observability.FrontendReportsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
        });

    /// <summary>
    /// Solo informes de la propia aplicación: si el navegador dice desde dónde llega la petición (<c>Sec-Fetch-Site</c>
    /// u <c>Origin</c>), tiene que ser el mismo origen. Evita que otra web use el reenvío desde el navegador de quien la visita.
    /// Con <c>Origin</c> se compara solo el nombre del host: nginx reenvía <c>Host</c> sin el puerto.
    /// </summary>
    private static bool IsSameOrigin(HttpRequest request)
    {
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        if (fetchSite.Length > 0 && !string.Equals(fetchSite, "same-origin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var origin = request.Headers.Origin.ToString();
        return origin.Length == 0
            || (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase));
    }

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

        if (!IsSameOrigin(request))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Solo se aceptan informes de esta aplicación.");
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
        var chunk = new byte[ReadChunkBytes];
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
/// <param name="SensitiveFields">Los mismos nombres de campo que filtra el backend (<see cref="SentryEventScrubber"/>).</param>
internal sealed record MonitoringConfigResponse(string? Dsn, string Environment, string Release, IReadOnlyList<string> SensitiveFields)
{
    public static MonitoringConfigResponse From(ObservabilityOptions options, AppVersion version) => new(
        string.IsNullOrEmpty(options.FrontendSentryDsn) ? null : options.FrontendSentryDsn,
        options.Environment,
        version.Value,
        SentryEventScrubber.SensitiveFieldHints);
}
