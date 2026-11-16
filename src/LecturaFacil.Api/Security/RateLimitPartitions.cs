// SPDX-License-Identifier: Apache-2.0
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace LecturaFacil.Api.Security;

/// <summary>Claves de partición y respuesta común de los límites de peticiones (US-16).</summary>
internal static class RateLimitPartitions
{
    /// <summary>
    /// Un cupo por usuario autenticado. Los límites se aplican después de la autorización, así que en las rutas con sesión
    /// siempre hay usuario; si no lo hubiera, se usa la IP para no compartir un cupo común.
    /// </summary>
    public static string User(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId
            ? "user:" + userId
            : "ip:" + ClientAddress.Of(context);

    /// <summary>Un cupo por ventana fija, sin cola: al agotarse responde 429 enseguida.</summary>
    public static RateLimitPartition<string> FixedWindow(string key, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
        });

    /// <summary>Al rechazar una petición, indica en <c>Retry-After</c> cuántos segundos faltan para el siguiente cupo.</summary>
    public static ValueTask AddRetryAfter(OnRejectedContext context, CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    }
}
