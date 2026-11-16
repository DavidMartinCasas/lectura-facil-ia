// SPDX-License-Identifier: Apache-2.0
namespace LecturaFacil.Api.Security;

/// <summary>
/// Dirección IP del cliente para los límites de peticiones. Detrás del nginx del despliegue es la que pone en
/// <c>X-Forwarded-For</c> (la sustituye, no la añade, así que el cliente no puede falsearla); sin proxy, la de la conexión.
/// La API no publica puertos en <c>docker-compose.yml</c>: solo se llega a ella a través de ese nginx.
/// </summary>
internal static class ClientAddress
{
    public static string Of(HttpContext context) =>
        context.Request.Headers["X-Forwarded-For"].ToString() is { Length: > 0 } forwarded
            ? forwarded
            : context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
}
