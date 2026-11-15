// SPDX-License-Identifier: Apache-2.0
using System.Net;

namespace LecturaFacil.Api.Tests;

public sealed class HealthEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_WhenDatabaseIsAvailable_Returns200WithHealthyStatus()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("""{"status":"Healthy"}""");
    }
}

public sealed class HealthEndpointWithoutDatabaseTests(ApiWithoutDatabaseFactory factory) : IClassFixture<ApiWithoutDatabaseFactory>
{
    [Fact]
    public async Task Health_WhenDatabaseIsUnavailable_Returns503WithoutInternalDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("""{"status":"Unhealthy"}""");
    }
}
