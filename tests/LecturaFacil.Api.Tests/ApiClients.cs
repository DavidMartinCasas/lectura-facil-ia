// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Api.Security;
using LecturaFacil.Api.Tests.Security;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LecturaFacil.Api.Tests;

/// <summary>Clientes HTTP de los tests: por HTTPS (la cookie de sesión es Secure) y con el token antiforgery ya puesto.</summary>
public static class ApiClients
{
    public static readonly Uri BaseAddress = new("https://localhost");

    /// <summary>Cliente con sesión de <paramref name="user"/> (por defecto, <see cref="TestUsers.Default"/>).</summary>
    public static Task<HttpClient> CreateApiClientAsync(this WebApplicationFactory<Program> factory, Guid? user = null) =>
        CreateWithAntiforgeryAsync(factory, (user ?? TestUsers.Default).ToString());

    /// <summary>Cliente sin sesión, pero con un token antiforgery válido para un usuario anónimo.</summary>
    public static Task<HttpClient> CreateAnonymousClientAsync(this WebApplicationFactory<Program> factory) =>
        CreateWithAntiforgeryAsync(factory, TestAuthenticationHandler.Anonymous);

    /// <summary>Cliente sin cabeceras de usuario: con el inicio de sesión real, la sesión la da la cookie.</summary>
    public static Task<HttpClient> CreateCookieClientAsync(this WebApplicationFactory<Program> factory) =>
        CreateWithAntiforgeryAsync(factory, userHeader: null);

    /// <summary>Pide un GET a /api para recibir la cookie <c>XSRF-TOKEN</c> y copia su valor en la cabecera.</summary>
    public static async Task RefreshAntiforgeryTokenAsync(this HttpClient client)
    {
        using var response = await client.GetAsync(TestAuthentication.AntiforgeryProbePath);
        client.UseAntiforgeryTokenFrom(response);
    }

    /// <summary>Si la respuesta trae un token antiforgery nuevo (login, logout o un GET), lo usa en las siguientes peticiones.</summary>
    public static void UseAntiforgeryTokenFrom(this HttpClient client, HttpResponseMessage response)
    {
        var token = ReadAntiforgeryCookie(response);
        if (token is null)
        {
            return;
        }

        client.DefaultRequestHeaders.Remove(AntiforgeryMiddleware.HeaderName);
        client.DefaultRequestHeaders.Add(AntiforgeryMiddleware.HeaderName, token);
    }

    private static async Task<HttpClient> CreateWithAntiforgeryAsync(WebApplicationFactory<Program> factory, string? userHeader)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = BaseAddress });
        if (userHeader is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, userHeader);
        }

        await client.RefreshAntiforgeryTokenAsync();
        return client;
    }

    private static string? ReadAntiforgeryCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var prefix = AntiforgeryMiddleware.CookieName + "=";
        var cookie = cookies.LastOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        return cookie is null ? null : Uri.UnescapeDataString(cookie[prefix.Length..].Split(';')[0]);
    }
}
