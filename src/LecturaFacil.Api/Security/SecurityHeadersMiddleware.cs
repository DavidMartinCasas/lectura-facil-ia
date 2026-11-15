// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Api.Security;

/// <summary>
/// Cabeceras de seguridad para todas las respuestas de la API. La API solo devuelve JSON,
/// así que la CSP no permite cargar nada ni incrustar la respuesta en otra página.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
