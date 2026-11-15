// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Api.Tests.Llm;

/// <summary><c>/dev/llm/ping</c>. No necesita base de datos y usa siempre <see cref="FakeLlmProvider"/>.</summary>
public sealed class LlmPingEndpointTests
{
    private static readonly Uri PingUri = new("/dev/llm/ping", UriKind.Relative);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Ping_OutsideDevelopment_Returns404AndNeverCallsTheProviders(string environment)
    {
        var provider = FakeLlmProvider.FromFixture("ping/ok");
        await using var factory = new FakeLlmApiFactory(environment, provider);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(PingUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ping_InDevelopment_ReturnsTheResultOfEachProviderAndRole()
    {
        var provider = FakeLlmProvider.FromFixture("ping/ok", "Principal");
        await using var factory = new FakeLlmApiFactory("Development", provider);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(PingUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var results = json.RootElement.EnumerateArray().ToArray();
        results.Length.ShouldBe(2);
        results.ShouldAllBe(result => result.GetProperty("success").GetBoolean());
        results[0].GetProperty("provider").GetString().ShouldBe("Principal");
        provider.Requests.Count.ShouldBe(2);
    }

    private sealed class FakeLlmApiFactory(string environment, FakeLlmProvider primary) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Database:ConnectionString", TestConfiguration.UnreachableDatabase);
            foreach (var (key, value) in TestConfiguration.ValidLlmSettings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services => services.AddTransient(_ => new LlmProviders(primary, null)));
        }
    }
}
