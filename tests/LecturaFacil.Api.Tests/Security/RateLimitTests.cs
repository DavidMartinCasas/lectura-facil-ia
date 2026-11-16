// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using LecturaFacil.Api.Tests.Documents;
using Microsoft.AspNetCore.Hosting;

namespace LecturaFacil.Api.Tests.Security;

/// <summary>Límites de peticiones por usuario (adaptaciones) y por IP (login) (US-16).</summary>
public sealed class RateLimitTests
{
    private const string ValidText = "Texto de prueba suficientemente largo para pasar la validación mínima de caracteres.";

    private static readonly Uri DocumentsUri = new("/api/documents", UriKind.Relative);
    private static readonly Uri LoginUri = new("/api/auth/login", UriKind.Relative);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Submit_AboveTheAdaptationsPerHour_Returns429WithRetryAfter()
    {
        await using var baseFactory = new DocumentsApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Limits:AdaptationsPerHour", "1"));
        using var client = await factory.CreateApiClientAsync();

        using var accepted = await client.PostAsJsonAsync(DocumentsUri, new { text = ValidText }, Token);
        using var rejected = await client.PostAsJsonAsync(DocumentsUri, new { text = ValidText }, Token);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var retryAfter = rejected.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldNotBeNull();
        retryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
        retryAfter.ShouldBeLessThanOrEqualTo(TimeSpan.FromHours(1));
        baseFactory.Repository.Added.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Submit_WhenAUserReachesTheLimit_OtherUsersCanStillSubmit()
    {
        await using var baseFactory = new DocumentsApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Limits:AdaptationsPerHour", "1"));
        using var first = await factory.CreateApiClientAsync(TestUsers.Default);
        using var second = await factory.CreateApiClientAsync(TestUsers.Other);

        using var firstAccepted = await first.PostAsJsonAsync(DocumentsUri, new { text = ValidText }, Token);
        using var firstRejected = await first.PostAsJsonAsync(DocumentsUri, new { text = ValidText }, Token);
        using var secondAccepted = await second.PostAsJsonAsync(DocumentsUri, new { text = ValidText }, Token);

        firstAccepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        firstRejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        secondAccepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Login_AboveTheLimitFromOneIp_Returns429OnlyForThatIp()
    {
        await using var baseFactory = new DocumentsApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Auth:LoginAttemptsPerMinute", "1"));
        using var client = await factory.CreateAnonymousClientAsync();

        using var first = await LoginFromAsync(client, "198.51.100.1");
        using var second = await LoginFromAsync(client, "198.51.100.1");
        using var otherIp = await LoginFromAsync(client, "198.51.100.2");

        first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.ShouldNotBeNull();
        otherIp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string ip)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LoginUri)
        {
            Content = JsonContent.Create(new { email = string.Empty, password = string.Empty }),
        };
        request.Headers.Add("X-Forwarded-For", ip);
        return await client.SendAsync(request, Token);
    }
}
