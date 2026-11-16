// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using LecturaFacil.Api.Auth;
using LecturaFacil.Api.Security;
using LecturaFacil.Api.Tests.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LecturaFacil.Api.Tests.Security;

/// <summary>Lo que se puede probar de /api/auth sin base de datos: validación, antiforgery, límite y /me.</summary>
public sealed class AuthEndpointTests
{
    private static readonly Uri LoginUri = new("/api/auth/login", UriKind.Relative);
    private static readonly Uri MeUri = new("/api/auth/me", UriKind.Relative);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Me_WithASession_ReturnsTheEmailOfTheUser()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateApiClientAsync(TestUsers.Other);

        var me = await client.GetFromJsonAsync<CurrentUserResponse>(MeUri, Token);

        me.ShouldNotBeNull().Email.ShouldBe($"{TestUsers.Other}@example.org");
    }

    [Fact]
    public async Task AnyApiGet_LeavesAReadableAntiforgeryCookieThatIsSecureAndStrict()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = ApiClients.BaseAddress });

        using var response = await client.GetAsync(MeUri, Token);

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(AntiforgeryMiddleware.CookieName + "=", StringComparison.Ordinal));
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        cookie.ShouldNotContain("httponly", Case.Insensitive);
    }

    [Fact]
    public async Task Antiforgery_BehindAProxyThatSpeaksHttp_StillWorks()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, TestAuthenticationHandler.Anonymous);
        await client.RefreshAntiforgeryTokenAsync();

        using var response = await client.PostAsJsonAsync(LoginUri, new { email = string.Empty, password = string.Empty }, Token);

        // Llega a la validación del cuerpo: el token antiforgery se ha aceptado.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(Token);
        body.ShouldContain("errors");
        body.ShouldNotContain(AntiforgeryMiddleware.InvalidTokenCode);
    }

    [Fact]
    public async Task AnonymousRequests_DoNotUseUpTheSubmitLimitOfUsersWithASession()
    {
        await using var baseFactory = new DocumentsApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Limits:AdaptationsPerHour", "1"));
        using var anonymous = await factory.CreateAnonymousClientAsync();
        using var client = await factory.CreateApiClientAsync();
        var text = new { text = new string('a', 100) };

        using var rejected = await anonymous.PostAsJsonAsync(new Uri("/api/documents", UriKind.Relative), text, Token);
        using var accepted = await client.PostAsJsonAsync(new Uri("/api/documents", UriKind.Relative), text, Token);

        rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Login_WithoutTheAntiforgeryToken_Returns400()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateAnonymousClientAsync();
        client.DefaultRequestHeaders.Remove(AntiforgeryMiddleware.HeaderName);

        using var response = await client.PostAsJsonAsync(LoginUri, new { email = "a@example.org", password = "una-contraseña-larga" }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain(AntiforgeryMiddleware.InvalidTokenCode);
    }

    [Theory]
    [InlineData(null, "una-contraseña-larga")]
    [InlineData("a@example.org", "")]
    public async Task Login_WithMissingFields_Returns400WithValidationErrors(string? email, string? password)
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateAnonymousClientAsync();

        using var response = await client.PostAsJsonAsync(LoginUri, new { email, password }, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Token)).ShouldContain("errors");
    }

    [Fact]
    public async Task Login_AboveTheRateLimit_Returns429()
    {
        await using var baseFactory = new DocumentsApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Auth:LoginAttemptsPerMinute", "1"));
        using var client = await factory.CreateAnonymousClientAsync();

        using var first = await client.PostAsJsonAsync(LoginUri, new { email = string.Empty, password = string.Empty }, Token);
        using var second = await client.PostAsJsonAsync(LoginUri, new { email = string.Empty, password = string.Empty }, Token);

        first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
