// SPDX-License-Identifier: Apache-2.0
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LecturaFacil.Api.Observability;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LecturaFacil.Api.Tests.Observability;

/// <summary>
/// El SDK de Sentry es global al proceso: otra API de los tests, sin DSN, lo deshabilitaría a mitad de prueba. Por eso
/// esta clase corre sola, después de las demás.
/// </summary>
[CollectionDefinition(nameof(SentryIsGlobal), DisableParallelization = true)]
public sealed class SentryIsGlobal;

[Collection(nameof(SentryIsGlobal))]
public sealed class ObservabilityEndpointTests(ObservabilityApiFactory factory) : IClassFixture<ObservabilityApiFactory>
{
    /// <summary>Frases del ejemplo y de la adaptación grabada que no pueden aparecer en ningún log ni evento.</summary>
    private static readonly string[] DocumentFragments =
    [
        "Concejalía de Vivienda",
        "2,5 veces el IPREM",
        "Ordenanza Municipal 7/2024",
        "Ayudas para pagar el alquiler",
        "Tiene 15 días para pedirla",
    ];

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Logs_WhenADocumentIsAdapted_ContainTheAdaptationTraceButNoFragmentOfTheText()
    {
        using var client = factory.CreateClient();
        var id = await SubmitExampleAsync(client);

        await WaitUntilAsync(() => factory.Repository.Added.Single(document => document.Id.Value == id).Status == DocumentStatus.Draft);
        var trace = await WaitForAsync(() => factory.Logs.Events.FirstOrDefault(log => log.Contains("\"EventId\":{\"Id\"", StringComparison.Ordinal) && log.Contains("AdaptationCompleted", StringComparison.Ordinal) && log.Contains(id.ToString(), StringComparison.Ordinal)));

        var json = JsonDocument.Parse(trace).RootElement;
        json.GetProperty("DocumentId").GetString().ShouldBe(id.ToString());
        json.GetProperty("ProviderRole").GetString().ShouldBe("primary");
        json.GetProperty("Provider").GetString().ShouldBe("Proveedor falso");
        json.GetProperty("Model").GetString().ShouldBe("modelo-adaptador-falso");
        json.GetProperty("InputTokens").GetInt64().ShouldBe(1200);
        json.GetProperty("OutputTokens").GetInt64().ShouldBe(900);
        json.TryGetProperty("DurationMs", out _).ShouldBeTrue();
        json.GetProperty("Result").GetString().ShouldBe("ok");
        factory.Logs.Events.ShouldAllBe(log => DocumentFragments.All(fragment => !log.Contains(fragment, StringComparison.Ordinal)));
        factory.Sentry.Envelopes.ShouldAllBe(envelope => DocumentFragments.All(fragment => !envelope.Contains(fragment, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task UnhandledException_ReachesSentryWithTheStackTraceButNoBodyCookiesAuthorizationEmailOrMessage()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ObservabilityApiFactory.ThrowingPath + "?email=ana@example.com")
        {
            Content = new StringContent("""{"text":"La Concejalía de Vivienda convoca ayudas."}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Cookie", "sesion=valor-de-la-cookie");
        request.Headers.Add("Authorization", "Bearer token-secreto");

        using var response = await client.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var envelope = await WaitForAsync(() => factory.Sentry.Envelopes.FirstOrDefault(sent => sent.Contains("TestPipelineStartupFilter", StringComparison.Ordinal)));
        envelope.ShouldContain("InvalidOperationException");
        envelope.ShouldNotContain(ObservabilityApiFactory.ExceptionText);
        envelope.ShouldContain("\"stacktrace\"");
        envelope.ShouldContain("\"environment\":\"pruebas\"");
        envelope.ShouldContain("\"release\":\"abc1234\"");
        envelope.ShouldNotContain("Concejalía");
        envelope.ShouldNotContain("valor-de-la-cookie");
        envelope.ShouldNotContain("token-secreto");
        envelope.ShouldNotContain("ana@example.com");
    }

    [Fact]
    public async Task ErrorLogs_ReachSentryAsEventsAndTheJsonConsoleLog()
    {
        using var client = factory.CreateClient();
        var logger = factory.Services.GetRequiredService<ILogger<ObservabilityEndpointTests>>();

#pragma warning disable CA1848, CA2254 // Log de prueba: no merece un LoggerMessage propio.
        logger.LogError("Error de prueba del worker US-17");
#pragma warning restore CA1848, CA2254

        await WaitForAsync(() => factory.Sentry.Envelopes.FirstOrDefault(sent => sent.Contains("Error de prueba del worker US-17", StringComparison.Ordinal)));
        factory.Logs.Events.ShouldContain(log => log.Contains("Error de prueba del worker US-17", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Metrics_FromTheInternalNetwork_ShowAdaptationsByProviderErrorsAndDuration()
    {
        using var client = factory.CreateClient();
        var id = await SubmitExampleAsync(client);
        await WaitUntilAsync(() => factory.Repository.Added.Single(document => document.Id.Value == id).Status == DocumentStatus.Draft);

        var metrics = await WaitForAsync(async () =>
        {
            using var request = MetricsRequest("172.18.0.10");
            using var response = await client.SendAsync(request, Token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
            var body = await response.Content.ReadAsStringAsync(Token);
            return body.Contains("lecturafacil_adaptations_total{provider_role=\"primary\",result=\"success\"}", StringComparison.Ordinal) ? body : null;
        });

        metrics.ShouldContain("# TYPE lecturafacil_adaptation_errors_total counter");
        metrics.ShouldContain("lecturafacil_adaptation_duration_seconds_count{provider_role=\"primary\",result=\"success\"}");
        metrics.ShouldNotContain(id.ToString());
    }

    [Theory]
    [InlineData("203.0.113.7", null)]
    [InlineData("172.18.0.10", "203.0.113.7")]
    [InlineData(null, null)]
    public async Task Metrics_FromOutsideOrThroughTheProxy_RespondsNotFound(string? remoteIp, string? forwardedFor)
    {
        using var client = factory.CreateClient();
        using var request = MetricsRequest(remoteIp);
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        using var response = await client.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Token)).ShouldNotContain("lecturafacil_");
    }

    [Fact]
    public async Task MonitoringConfig_GivesTheBrowserOnlyTheFrontendDsnEnvironmentAndRelease()
    {
        using var client = factory.CreateClient();

        var config = await client.GetFromJsonAsync<JsonElement>(new Uri("/api/monitoring/config", UriKind.Relative), Token);

        config.GetProperty("dsn").GetString().ShouldBe(ObservabilityApiFactory.FrontendDsn);
        config.GetProperty("environment").GetString().ShouldBe("pruebas");
        config.GetProperty("release").GetString().ShouldBe("abc1234");
        config.GetProperty("sensitiveFields").EnumerateArray().Select(field => field.GetString())
            .ShouldBe(SentryEventScrubber.SensitiveFieldHints);
        config.GetRawText().ShouldNotContain("clave-backend");
    }

    [Fact]
    public async Task Health_IncludesTheDeployedVersionInAHeaderAndKeepsItsBody()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), Token);

        response.Headers.GetValues("X-App-Version").ShouldBe(["abc1234"]);
        (await response.Content.ReadAsStringAsync(Token)).ShouldBe("""{"status":"Unhealthy"}""");
    }

    [Fact]
    public async Task Tunnel_ForwardsBrowserReportsForTheFrontendDsnToItsProject()
    {
        using var client = factory.CreateClient();
        var envelope = $"{{\"dsn\":\"{ObservabilityApiFactory.FrontendDsn}\"}}\n{{\"type\":\"event\"}}\n{{\"message\":\"fallo del navegador\"}}";

        using var response = await client.PostAsync(
            new Uri("/api/monitoring/sentry", UriKind.Relative),
            new StringContent(envelope, Encoding.UTF8, "text/plain"),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Sentry.Forwarded.ShouldContain(sent =>
            sent.Target == new Uri("https://sentry.example.com/api/2/envelope/") && sent.Body == envelope);
    }

    [Fact]
    public async Task Tunnel_RejectsReportsForAnyOtherDsn()
    {
        using var client = factory.CreateClient();
        var envelope = "{\"dsn\":\"https://clave@atacante.example.com/9\"}\n{\"type\":\"event\"}\n{}";

        using var response = await client.PostAsync(
            new Uri("/api/monitoring/sentry", UriKind.Relative),
            new StringContent(envelope, Encoding.UTF8, "text/plain"),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        factory.Sentry.Forwarded.ShouldNotContain(sent => sent.Target.Host == "atacante.example.com");
    }

    [Theory]
    [InlineData("Sec-Fetch-Site", "cross-site")]
    [InlineData("Origin", "https://otra-web.example.com")]
    public async Task Tunnel_RejectsReportsFromAnotherOrigin(string header, string value)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitoring/sentry")
        {
            Content = new StringContent($"{{\"dsn\":\"{ObservabilityApiFactory.FrontendDsn}\"}}\n{{\"type\":\"event\"}}\n{{}}", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add(header, value);
        request.Headers.Add("X-Forwarded-For", $"198.51.100.{header.Length}");

        using var response = await client.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Tunnel_AcceptsReportsFromItsOwnOriginEvenWhenTheProxyDropsThePort()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitoring/sentry")
        {
            Content = new StringContent($"{{\"dsn\":\"{ObservabilityApiFactory.FrontendDsn}\"}}\n{{\"type\":\"event\"}}\n{{}}", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("Origin", "http://localhost:8080");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        request.Headers.Add("X-Forwarded-For", "198.51.100.150");

        using var response = await client.SendAsync(request, Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tunnel_LimitsReportsPerClientWithoutBlockingOthers()
    {
        using var client = factory.CreateClient();
        var limit = new ObservabilityOptions().FrontendReportsPerMinute;

        var statuses = new List<HttpStatusCode>();
        for (var sent = 0; sent <= limit; sent++)
        {
            statuses.Add(await PostReportAsync(client, "198.51.100.200"));
        }

        statuses.Take(limit).ShouldAllBe(status => status == HttpStatusCode.OK);
        statuses[^1].ShouldBe(HttpStatusCode.TooManyRequests);
        (await PostReportAsync(client, "198.51.100.201")).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tunnel_RejectsReportsLargerThanTheLimit()
    {
        using var client = factory.CreateClient();
        var envelope = $"{{\"dsn\":\"{ObservabilityApiFactory.FrontendDsn}\"}}\n" + new string('x', new ObservabilityOptions().MaxFrontendReportBytes);

        using var response = await client.PostAsync(
            new Uri("/api/monitoring/sentry", UriKind.Relative),
            new StringContent(envelope, Encoding.UTF8, "text/plain"),
            Token);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    private static async Task<HttpStatusCode> PostReportAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/monitoring/sentry")
        {
            Content = new StringContent($"{{\"dsn\":\"{ObservabilityApiFactory.FrontendDsn}\"}}\n{{\"type\":\"event\"}}\n{{}}", Encoding.UTF8, "text/plain"),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request, Token);
        return response.StatusCode;
    }

    private static HttpRequestMessage MetricsRequest(string? remoteIp)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        if (remoteIp is not null)
        {
            request.Headers.Add(ObservabilityApiFactory.RemoteIpHeader, remoteIp);
        }

        return request;
    }

    private static async Task<Guid> SubmitExampleAsync(HttpClient client)
    {
        using var submitted = await client.PostAsync(
            new Uri("/api/documents", UriKind.Relative),
            new StringContent(await File.ReadAllTextAsync(ExampleFile(), Token), Encoding.UTF8, "application/json"),
            Token);
        submitted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return (await submitted.Content.ReadFromJsonAsync<JsonElement>(Token)).GetProperty("id").GetGuid();
    }

    private static Task WaitUntilAsync(Func<bool> condition) => WaitForAsync(() => condition() ? "ok" : null);

    private static Task<string> WaitForAsync(Func<string?> probe) => WaitForAsync(() => Task.FromResult(probe()));

    private static async Task<string> WaitForAsync(Func<Task<string?>> probe)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(Timeout);
        while (true)
        {
            if (await probe() is { } value)
            {
                return value;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), timeout.Token);
        }
    }

    private static string ExampleFile()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures", "ejemplo-iprem.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("No se encuentra tests/fixtures/ejemplo-iprem.json.");
    }
}
