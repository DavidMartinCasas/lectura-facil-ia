// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Infrastructure.Observability;

/// <summary>
/// La URL de ingesta de sobres (<c>envelope</c>) de un DSN de Sentry: <c>https://clave@servidor[:puerto]/[ruta/]proyecto</c>
/// → <c>https://servidor[:puerto]/[ruta/]api/proyecto/envelope/</c>. Solo admite <c>https</c>, o <c>http</c> hacia
/// <c>localhost</c> para pruebas locales.
/// </summary>
public static class SentryEndpoint
{
    public static Uri? TryParse(string dsn)
    {
        if (!Uri.TryCreate(dsn, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !(uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
        {
            return null;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        var lastSlash = path.LastIndexOf('/');
        var projectId = path[(lastSlash + 1)..];
        if (projectId.Length == 0 || !projectId.All(char.IsAsciiDigit))
        {
            return null;
        }

        var prefix = path[..lastSlash];
        return new UriBuilder(uri.Scheme, uri.Host, uri.Port, $"{prefix}/api/{projectId}/envelope/").Uri;
    }
}
