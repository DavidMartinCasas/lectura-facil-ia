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

    /// <summary>
    /// Tipos de item que se reenvían: errores, sesiones e informes del propio SDK. Nada de adjuntos, grabaciones ni
    /// perfiles, que podrían llevar contenido de la página o gastar cuota.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedItemTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "event", "session", "sessions", "client_report",
    };

    /// <summary>Items como mucho por sobre: el SDK del navegador manda uno o dos.</summary>
    public const int MaxItemsPerEnvelope = 10;

    public bool IsEnabled => !string.IsNullOrEmpty(options.Value.FrontendSentryDsn);

    public async Task<Result> ForwardAsync(ReadOnlyMemory<byte> envelope, CancellationToken cancellationToken)
    {
        var dsn = options.Value.FrontendSentryDsn;
        if (string.IsNullOrEmpty(dsn) || SentryEndpoint.TryParse(dsn) is not { } target)
        {
            return Result.Failure(Disabled);
        }

        if (!string.Equals(EnvelopeDsn(envelope.Span), dsn, StringComparison.Ordinal) || !HasOnlyAllowedItems(envelope.Span))
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
    private static string? EnvelopeDsn(ReadOnlySpan<byte> envelope) =>
        StringProperty(NextLine(ref envelope), "dsn");

    /// <summary>
    /// Recorre los items del sobre (cabecera JSON en una línea y su contenido, de <c>length</c> bytes o hasta el
    /// siguiente salto de línea) y comprueba que todos son de un tipo permitido y que no hay demasiados.
    /// </summary>
    private static bool HasOnlyAllowedItems(ReadOnlySpan<byte> envelope)
    {
        NextLine(ref envelope);
        var items = 0;
        while (!envelope.IsEmpty)
        {
            var header = NextLine(ref envelope);
            if (header.IsEmpty && envelope.IsEmpty)
            {
                break;
            }

            if (++items > MaxItemsPerEnvelope
                || StringProperty(header, "type") is not { } type
                || !AllowedItemTypes.Contains(type)
                || !SkipPayload(ref envelope, Length(header)))
            {
                return false;
            }
        }

        return items > 0;
    }

    private static bool SkipPayload(ref ReadOnlySpan<byte> envelope, long? length)
    {
        if (length is null)
        {
            NextLine(ref envelope);
            return true;
        }

        if (length < 0 || length > envelope.Length)
        {
            return false;
        }

        envelope = envelope[(int)length.Value..];
        if (!envelope.IsEmpty && envelope[0] == (byte)'\n')
        {
            envelope = envelope[1..];
        }

        return true;
    }

    private static ReadOnlySpan<byte> NextLine(ref ReadOnlySpan<byte> envelope)
    {
        var newline = envelope.IndexOf((byte)'\n');
        var line = newline < 0 ? envelope : envelope[..newline];
        envelope = newline < 0 ? [] : envelope[(newline + 1)..];
        return line;
    }

    private static string? StringProperty(ReadOnlySpan<byte> json, string name) =>
        Property(json, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static long? Length(ReadOnlySpan<byte> json) =>
        Property(json, "length") is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var length) ? length : null;

    private static JsonElement? Property(ReadOnlySpan<byte> json, string name)
    {
        try
        {
            var reader = new Utf8JsonReader(json);
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(name, out var value)
                ? value.Clone()
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
