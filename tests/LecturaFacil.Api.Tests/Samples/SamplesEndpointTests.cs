// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LecturaFacil.Api.Tests.Documents;

namespace LecturaFacil.Api.Tests.Samples;

/// <summary><c>GET /api/samples</c> (US-16): 3 textos de ejemplo, solo con sesión, que se pueden enviar tal cual.</summary>
public sealed class SamplesEndpointTests
{
    private static readonly Uri SamplesUri = new("/api/samples", UriKind.Relative);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task List_WithASession_ReturnsThreeSamplesWithTheirSource()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(SamplesUri, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        var samples = json.RootElement.EnumerateArray().ToList();
        samples.Count.ShouldBe(3);
        samples.ShouldAllBe(sample => !string.IsNullOrWhiteSpace(sample.GetProperty("source").GetString()));
    }

    [Fact]
    public async Task List_WithoutASession_Returns401()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateAnonymousClientAsync();

        using var response = await client.GetAsync(SamplesUri, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Submit_EverySample_IsAccepted()
    {
        await using var factory = new DocumentsApiFactory();
        using var client = await factory.CreateApiClientAsync();
        using var json = JsonDocument.Parse(await client.GetStringAsync(SamplesUri, Token));

        foreach (var sample in json.RootElement.EnumerateArray())
        {
            var body = new { title = sample.GetProperty("title").GetString(), text = sample.GetProperty("text").GetString() };
            using var response = await client.PostAsJsonAsync(new Uri("/api/documents", UriKind.Relative), body, Token);
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        factory.Repository.Added.Count.ShouldBe(3);
    }
}
