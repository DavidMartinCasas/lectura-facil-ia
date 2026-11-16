// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using LecturaFacil.Api.Tests.Documents;
using Microsoft.AspNetCore.Hosting;

namespace LecturaFacil.Api.Tests.Configuration;

/// <summary><c>GET /api/config/public</c> (US-16): aviso y Turnstile, sin ningún secreto.</summary>
public sealed class PublicConfigEndpointTests
{
    private const string SecretKey = "clave-secreta-que-no-debe-salir";

    private static readonly Uri ConfigUri = new("/api/config/public", UriKind.Relative);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ByDefault_HasNoNoticeAndTurnstileDisabled()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateAnonymousClientAsync();

        using var json = await GetJsonAsync(client);

        json.RootElement.GetProperty("notice").GetString().ShouldBe(string.Empty);
        json.RootElement.GetProperty("turnstileEnabled").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("turnstileSiteKey").GetString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Get_WithoutSession_ReturnsTheNoticeAndTheSiteKeyButNeverTheSecretKey()
    {
        await using var baseFactory = new DocumentsApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Ui:Notice", "Instancia de demostración: no subas datos personales.");
            builder.UseSetting("Turnstile:Enabled", "true");
            builder.UseSetting("Turnstile:SiteKey", "clave-publica");
            builder.UseSetting("Turnstile:SecretKey", SecretKey);
        });
        using var client = await factory.CreateAnonymousClientAsync();

        using var response = await client.GetAsync(ConfigUri, Token);
        var body = await response.Content.ReadAsStringAsync(Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.ShouldNotContain(SecretKey);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("notice").GetString().ShouldBe("Instancia de demostración: no subas datos personales.");
        json.RootElement.GetProperty("turnstileEnabled").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("turnstileSiteKey").GetString().ShouldBe("clave-publica");
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client)
    {
        using var response = await client.GetAsync(ConfigUri, Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }
}
