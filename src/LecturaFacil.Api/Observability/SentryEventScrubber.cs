// SPDX-License-Identifier: Apache-2.0
using Sentry;

namespace LecturaFacil.Api.Observability;

/// <summary>
/// Filtro <c>BeforeSend</c> y <c>BeforeBreadcrumb</c> de Sentry (US-17): ningún evento sale con el cuerpo de la
/// petición, cookies, cabeceras (salvo unas pocas inofensivas), la consulta de la URL, datos de la persona usuaria ni
/// ningún campo cuyo nombre sugiera texto de un documento. Es la segunda barrera: la primera es no registrar texto.
/// </summary>
internal static class SentryEventScrubber
{
    public const string Redacted = "[filtrado]";

    /// <summary>
    /// Partes de nombres de campo que pueden llevar texto de un documento o datos de la persona. Es la única lista: el
    /// frontend la recibe en <c>/api/monitoring/config</c> para filtrar igual.
    /// </summary>
    public static readonly IReadOnlyList<string> SensitiveFieldHints = ["text", "content", "original", "adapted", "body", "email", "cookie", "authorization", "token", "password", "secret"];

    /// <summary>Cabeceras que no llevan datos de la persona ni credenciales.</summary>
    private static readonly HashSet<string> SafeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept", "Content-Type", "Content-Length", "User-Agent", "Host",
    };

    public static SentryEvent Scrub(SentryEvent sentryEvent)
    {
        ArgumentNullException.ThrowIfNull(sentryEvent);

        ScrubRequest(sentryEvent.Request);
        ScrubExceptionMessages(sentryEvent);
        sentryEvent.User = new SentryUser();
        sentryEvent.ServerName = null;

        foreach (var key in sentryEvent.Extra.Keys.Where(IsSensitive).ToList())
        {
            sentryEvent.SetExtra(key, Redacted);
        }

        foreach (var key in sentryEvent.Tags.Keys.Where(IsSensitive).ToList())
        {
            sentryEvent.UnsetTag(key);
        }

        foreach (var key in sentryEvent.Contexts.Keys.Where(IsSensitive).ToList())
        {
            sentryEvent.Contexts.Remove(key);
        }

        return sentryEvent;
    }

    public static Breadcrumb Scrub(Breadcrumb breadcrumb)
    {
        ArgumentNullException.ThrowIfNull(breadcrumb);
        if (breadcrumb.Data is not { Count: > 0 } data || !data.Keys.Any(IsSensitive))
        {
            return breadcrumb;
        }

        var safe = data.ToDictionary(pair => pair.Key, pair => IsSensitive(pair.Key) ? Redacted : pair.Value, StringComparer.Ordinal);
        return new Breadcrumb(breadcrumb.Message ?? string.Empty, breadcrumb.Type ?? string.Empty, safe, breadcrumb.Category, breadcrumb.Level);
    }

    public static bool IsSensitive(string fieldName) =>
        SensitiveFieldHints.Any(hint => fieldName.Contains(hint, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// El mensaje de una excepción de una librería puede citar la entrada (por ejemplo, un fragmento del texto). Se
    /// sustituye por el tipo; la traza de pila se mantiene, que es lo que sirve para depurar.
    /// </summary>
    private static void ScrubExceptionMessages(SentryEvent sentryEvent)
    {
        if (sentryEvent.SentryExceptions is not { } exceptions || !exceptions.Any())
        {
            return;
        }

        foreach (var exception in exceptions)
        {
            exception.Value = exception.Type;
        }

        if (sentryEvent.Message is { } message)
        {
            message.Formatted = null;
            message.Params = null;
        }
    }

    private static void ScrubRequest(SentryRequest request)
    {
        request.Data = null;
        request.Cookies = null;
        request.QueryString = null;
        request.Env.Clear();
        request.Other.Clear();
        foreach (var name in request.Headers.Keys.Where(name => !SafeHeaders.Contains(name)).ToList())
        {
            request.Headers.Remove(name);
        }

        if (request.Url is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            request.Url = uri.GetLeftPart(UriPartial.Path);
        }
    }
}
