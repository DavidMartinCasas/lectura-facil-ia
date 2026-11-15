// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Sockets;

namespace LecturaFacil.Infrastructure.Llm;

/// <summary>
/// Reglas para la URL de un proveedor: https siempre; http solo hacia localhost o una IP privada, para proveedores
/// locales. Un nombre de host con http no se admite aunque no tenga puntos: el DNS podría resolverlo a una IP pública
/// (dominios de búsqueda) y la clave y los documentos viajarían sin cifrar.
/// </summary>
public static class ProviderEndpoint
{
    public static bool IsAllowed(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp && IsLocalNetwork(uri));
    }

    private static bool IsLocalNetwork(Uri uri)
    {
        if (uri.IsLoopback)
        {
            return true;
        }

        return uri.HostNameType switch
        {
            UriHostNameType.IPv4 or UriHostNameType.IPv6 => IsPrivate(IPAddress.Parse(uri.DnsSafeHost)),
            _ => false,
        };
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6UniqueLocal;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
