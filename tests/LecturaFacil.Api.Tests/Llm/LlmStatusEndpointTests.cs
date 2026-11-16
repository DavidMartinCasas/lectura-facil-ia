// SPDX-License-Identifier: Apache-2.0
using System.Net;
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Api.Tests.Llm;

/// <summary><c>GET /api/status/llm</c>. No necesita base de datos ni llama a ningún proveedor.</summary>
public sealed class LlmStatusEndpointTests
{
    private static readonly Uri StatusUri = new("/api/status/llm", UriKind.Relative);

    [Fact]
    public async Task Status_WhenFallbackIsDisabled_ShowsOnlyThePrimary()
    {
        var availability = new LlmAvailability(fallbackEnabled: false);
        await using var factory = new StatusApiFactory(availability);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(StatusUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe("""{"primary":{"available":true},"fallback":null}""");
    }

    [Fact]
    public async Task Status_WhenPrimaryIsDown_ShowsItWithoutUrlsModelsOrNames()
    {
        var availability = new LlmAvailability(fallbackEnabled: true);
        availability.Update(new LlmAvailabilityStatus(PrimaryAvailable: false, FallbackAvailable: true));
        await using var factory = new StatusApiFactory(availability);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(StatusUri, TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("""{"primary":{"available":false},"fallback":{"available":true}}""");
        foreach (var value in TestConfiguration.ValidLlmSettings
                     .Where(setting => setting.Key.StartsWith("Llm:Primary:", StringComparison.Ordinal))
                     .Select(setting => setting.Value))
        {
            body.ShouldNotContain(value, Case.Insensitive);
        }
    }

    [Theory]
    [InlineData(true, "Healthy")]
    [InlineData(false, "Degraded")]
    public async Task LlmHealth_ReflectsThePrimaryWithoutDetailsAndWithoutTheDatabase(bool primaryAvailable, string expected)
    {
        var availability = new LlmAvailability(fallbackEnabled: false);
        availability.Update(new LlmAvailabilityStatus(primaryAvailable, null));
        await using var factory = new StatusApiFactory(availability);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/llm", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe($$"""{"status":"{{expected}}"}""");
    }

    private sealed class StatusApiFactory(LlmAvailability availability) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Database:ConnectionString", TestConfiguration.UnreachableDatabase);
            foreach (var (key, value) in TestConfiguration.ValidLlmSettings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services => services.AddSingleton(availability));
        }
    }
}
