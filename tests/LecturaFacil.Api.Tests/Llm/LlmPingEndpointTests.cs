// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Text.Json;
using LecturaFacil.Api.Llm;
using LecturaFacil.Api.Tests.Security;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Testing;
using Microsoft.AspNetCore.Builder;
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
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(PingUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ping_InDevelopmentFromAnotherMachine_Returns404AndNeverCallsTheProviders()
    {
        var provider = FakeLlmProvider.FromFixture("ping/ok");
        await using var factory = new FakeLlmApiFactory("Development", provider, IPAddress.Parse("192.168.1.20"));
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(PingUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ping_InDevelopmentAboveTheRateLimit_Returns429WithoutCallingTheProviders()
    {
        var provider = FakeLlmProvider.FromFixture("ping/ok");
        await using var factory = new FakeLlmApiFactory("Development", provider);
        using var client = await factory.CreateApiClientAsync();

        for (var i = 0; i < LlmDiagnosticsEndpoints.PingsPerWindow; i++)
        {
            using var allowed = await client.GetAsync(PingUri, TestContext.Current.CancellationToken);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var response = await client.GetAsync(PingUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        provider.Requests.Count.ShouldBe(LlmDiagnosticsEndpoints.PingsPerWindow * 2);
    }

    [Fact]
    public async Task Ping_InDevelopment_ReturnsTheResultOfEachProviderAndRole()
    {
        var provider = FakeLlmProvider.FromFixture("ping/ok", "Principal");
        await using var factory = new FakeLlmApiFactory("Development", provider);
        using var client = await factory.CreateApiClientAsync();

        using var response = await client.GetAsync(PingUri, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var results = json.RootElement.EnumerateArray().ToArray();
        results.Length.ShouldBe(2);
        results.ShouldAllBe(result => result.GetProperty("success").GetBoolean());
        results[0].GetProperty("provider").GetString().ShouldBe("Principal");
        provider.Requests.Count.ShouldBe(2);
    }

    /// <summary>API con <see cref="FakeLlmProvider"/> y peticiones que llegan desde <paramref name="remoteAddress"/> (localhost por defecto).</summary>
    private sealed class FakeLlmApiFactory(string environment, FakeLlmProvider primary, IPAddress? remoteAddress = null)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Database:ConnectionString", TestConfiguration.UnreachableDatabase);
            foreach (var (key, value) in TestConfiguration.ValidLlmSettings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                services.AddTransient(_ => new LlmProviders(primary, null));
                services.UseTestAuthentication();
                services.AddSingleton<IStartupFilter>(new RemoteAddressStartupFilter(remoteAddress ?? IPAddress.Loopback));
            });
        }
    }

    private sealed class RemoteAddressStartupFilter(IPAddress remoteAddress) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = remoteAddress;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
