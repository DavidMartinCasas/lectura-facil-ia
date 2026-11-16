// SPDX-License-Identifier: Apache-2.0
using Microsoft.AspNetCore.Antiforgery;

namespace LecturaFacil.Api.Security;

/// <summary>
/// Protección antiforgery de doble envío para <c>/api</c>: cada petición de lectura deja el token en la cookie
/// <see cref="CookieName"/> (legible por el frontend, no HttpOnly) y toda petición que cambia estado debe devolverlo en la
/// cabecera <see cref="HeaderName"/>. Se aplica a todas las rutas, no endpoint a endpoint, para que una ruta nueva quede
/// protegida sin acordarse de ello.
/// </summary>
internal static class AntiforgeryMiddleware
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";
    public const string InvalidTokenCode = "auth.antiforgery";

    private const string ApiPath = "/api";

    /// <summary>Antes de la autorización: también el formulario de login (sin sesión) necesita el token.</summary>
    public static IApplicationBuilder UseAntiforgeryTokenCookie(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (IsApi(context) && IsSafe(context.Request.Method))
            {
                IssueToken(context);
            }

            return next(context);
        });

    /// <summary>Después de la autorización: una petición sin sesión a una ruta protegida responde 401, no 400.</summary>
    public static IApplicationBuilder UseAntiforgeryValidation(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (IsApi(context) && !IsSafe(context.Request.Method)
                && !await context.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(context))
            {
                await TypedResults.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        detail: "La petición no es válida. Recarga la página e inténtalo de nuevo.",
                        extensions: new Dictionary<string, object?> { ["code"] = InvalidTokenCode })
                    .ExecuteAsync(context);
                return;
            }

            await next(context);
        });

    /// <summary>
    /// Genera el token para el usuario actual de <paramref name="context"/>. Tras iniciar o cerrar sesión hay que volver a
    /// generarlo: el token va ligado a la identidad.
    /// </summary>
    public static void IssueToken(HttpContext context)
    {
        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        context.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
    }

    private static bool IsApi(HttpContext context) => context.Request.Path.StartsWithSegments(ApiPath, StringComparison.OrdinalIgnoreCase);

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
