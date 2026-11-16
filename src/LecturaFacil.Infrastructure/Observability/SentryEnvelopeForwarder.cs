// SPDX-License-Identifier: Apache-2.0
using System.Net.Http.Headers;
using System.Text.Json;
using LecturaFacil.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Observability;

/// <summary>
/// Reenvía a Sentry los informes de errores del navegador (el <c>tunnel</c> del SDK de Sentry). Así el navegador solo
/// habla con su propio origen y la CSP sigue con <c>connect-src 'self'</c>. Solo reenvía sobres cuyo DSN es exactamente
/// el del frontend configurado, y siempre al servidor de ese DSN: la petición no puede elegir el destino (SSRF).
/// </summary>
public sealed partial class SentryEnvelopeForwarder(
    IHttpClientFactory httpClients,
    IOptions<ObservabilityOptions> options,
    ILogger<SentryEnvelopeForwarder> logger)
{
    public const string HttpClientName = "sentry-tunnel";
    public const string EnvelopeContentType = "application/x-sentry-envelope";

    /// <summary>Tiempo máximo para entregar un informe: el navegador no espera la respuesta, pero no se acumulan conexiones.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static readonly Error Disabled = new("monitoring.disabled", "No hay informes de errores configurados.");
    public static readonly Error InvalidEnvelope = new("monitoring.invalid_envelope", "El informe de errores no es válido.");
    public static readonly Error Unavailable = new("monitoring.unavailable", "No se ha podido enviar el informe de errores.");

    public bool IsEnabled => !string.IsNullOrEmpty(options.Value.FrontendSentryDsn);

    public async Task<Result> ForwardAsync(ReadOnlyMemory<byte> envelope, CancellationToken cancellationToken)
    {
        var dsn = options.Value.FrontendSentryDsn;
        if (string.IsNullOrEmpty(dsn) || SentryEndpoint.TryParse(dsn) is not { } target)
        {
            return Result.Failure(Disabled);
        }

        if (!string.Equals(EnvelopeDsn(envelope.Span), dsn, StringComparison.Ordinal))
        {
            return Result.Failure(InvalidEnvelope);
        }

        try
        {
            using var content = new ReadOnlyMemoryContent(envelope);
            content.Headers.ContentType = new MediaTypeHeaderValue(EnvelopeContentType);
            using var response = await httpClients.CreateClient(HttpClientName).PostAsync(target, content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            LogRejected((int)response.StatusCode);
            return Result.Failure(Unavailable);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogFailed(exception.GetType().Name);
            return Result.Failure(Unavailable);
        }
    }

    /// <summary>El <c>dsn</c> de la cabecera del sobre: la primera línea, un objeto JSON.</summary>
    private static string? EnvelopeDsn(ReadOnlySpan<byte> envelope)
    {
        var newline = envelope.IndexOf((byte)'\n');
        var header = newline < 0 ? envelope : envelope[..newline];
        try
        {
            var reader = new Utf8JsonReader(header);
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("dsn", out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sentry rejected a browser error report with HTTP {StatusCode}.")]
    private partial void LogRejected(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A browser error report could not be forwarded to Sentry ({ExceptionType}).")]
    private partial void LogFailed(string exceptionType);
}
