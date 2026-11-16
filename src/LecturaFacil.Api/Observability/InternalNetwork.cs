// SPDX-License-Identifier: Apache-2.0
using System.Net;

namespace LecturaFacil.Api.Observability;

/// <summary>
/// Si una petición viene de la red interna: <c>localhost</c> o una dirección privada (10/8, 172.16/12, 192.168/16,
/// 100.64/10, fc00::/7 o de enlace local), y sin cabeceras de proxy. Lo que llega a través del proxy público
/// (nginx pone <c>X-Forwarded-For</c>) nunca es interno, aunque el proxy esté en la misma red.
/// </summary>
internal static class InternalNetwork
{
    private static readonly IPNetwork[] PrivateNetworks =
    [
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
    ];

    private static readonly string[] ProxyHeaders = ["X-Forwarded-For", "X-Forwarded-Host", "X-Real-IP", "Forwarded"];

    public static bool IsInternal(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return !ProxyHeaders.Any(context.Request.Headers.ContainsKey)
            && context.Connection.RemoteIpAddress is { } address
            && IsInternal(address);
    }

    public static bool IsInternal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return IPAddress.IsLoopback(ip) || PrivateNetworks.Any(network => network.Contains(ip));
    }
}
