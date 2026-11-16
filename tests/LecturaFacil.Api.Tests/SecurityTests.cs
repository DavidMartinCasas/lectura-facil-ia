// SPDX-License-Identifier: Apache-2.0
using System.Net;

namespace LecturaFacil.Api.Tests;

public sealed class SecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/no-existe")]
    [InlineData(ApiFactory.ThrowingPath)]
    public async Task AnyResponse_WhenReturned_IncludesSecurityHeaders(string path)
    {
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        var headers = response.Headers;
        headers.GetValues("Content-Security-Policy").ShouldHaveSingleItem().ShouldBe("default-src 'none'; frame-ancestors 'none'");
        headers.GetValues("X-Content-Type-Options").ShouldHaveSingleItem().ShouldBe("nosniff");
        headers.GetValues("X-Frame-Options").ShouldHaveSingleItem().ShouldBe("DENY");
        headers.GetValues("Referrer-Policy").ShouldHaveSingleItem().ShouldBe("no-referrer");
        headers.Contains("Permissions-Policy").ShouldBeTrue();
        headers.Contains("Server").ShouldBeFalse();
    }

    [Fact]
    public async Task UnhandledException_OutsideDevelopment_ReturnsGenericProblemDetails()
    {
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(new Uri(ApiFactory.ThrowingPath, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("InvalidOperationException");
        body.ShouldNotContain("/srv/secreto");
        body.ShouldNotContain("   at ");
    }

    [Fact]
    public async Task UnknownRoute_WhenRequested_ReturnsProblemDetails()
    {
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(new Uri("/no-existe", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task OpenApiDocument_OutsideDevelopment_IsNotPublished()
    {
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

public sealed class DevelopmentTests(DevelopmentApiFactory factory) : IClassFixture<DevelopmentApiFactory>
{
    [Fact]
    public async Task OpenApiDocument_InDevelopment_IsPublished()
    {
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
