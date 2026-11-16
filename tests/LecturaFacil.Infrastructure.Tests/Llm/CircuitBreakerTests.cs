// SPDX-License-Identifier: Apache-2.0
using System.Net;
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Infrastructure.Tests.Llm;

/// <summary>Tubería de resiliencia del cliente HTTP del principal: reintento corto y circuit breaker.</summary>
public sealed class CircuitBreakerTests
{
    [Fact]
    public async Task Primary_WhenItFailsSeveralTimes_OpensTheCircuitAndStopsSendingRequests()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.ServiceUnavailable, "{}");
        using var services = Services(handler, Breaker());
        var primary = services.GetRequiredService<LlmProviders>().Primary;

        await CallAsync(primary);
        await CallAsync(primary);
        var whileOpen = await CallAsync(primary);

        handler.Requests.Count.ShouldBe(2);
        whileOpen.Error.ShouldBe(LlmErrors.ProviderUnavailable);
    }

    [Fact]
    public async Task Primary_AfterTheBreakDuration_LetsATrialRequestThroughAndRecovers()
    {
        var failures = 2;
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(failures-- > 0
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FakeHttpMessageHandler.ChatCompletion("""{"ok":true}""", "modelo")),
            }));
        using var services = Services(handler, Breaker());
        var primary = services.GetRequiredService<LlmProviders>().Primary;
        await CallAsync(primary);
        await CallAsync(primary);

        await Task.Delay(TimeSpan.FromSeconds(Breaker().BreakDurationSeconds + 0.5), TestContext.Current.CancellationToken);
        var result = await CallAsync(primary);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Primary_WhenATransientErrorHappensOnce_RetriesTheSameRequest()
    {
        var failures = 1;
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(failures-- > 0
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FakeHttpMessageHandler.ChatCompletion("""{"ok":true}""", "modelo")),
            }));
        using var services = Services(handler, LlmRegistrationTests.FastResilience());

        var result = await CallAsync(services.GetRequiredService<LlmProviders>().Primary);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[1].Body.ShouldBe(handler.Requests[0].Body);
    }

    [Fact]
    public async Task Fallback_HasNoRetries()
    {
        var options = LlmRegistrationTests.Options(LlmProviderType.OpenAICompatible);
        options.Fallback = LlmRegistrationTests.Fallback();
        var fallbackHandler = FakeHttpMessageHandler.Returning(HttpStatusCode.ServiceUnavailable, "{}");
        using var services = LlmRegistrationTests.BuildServices(
            options,
            name => name == LlmProviderFactory.FallbackHttpClient
                ? fallbackHandler
                : FakeHttpMessageHandler.Returning(HttpStatusCode.ServiceUnavailable, "{}"));

        await CallAsync(services.GetRequiredService<LlmProviders>().Fallback!);

        fallbackHandler.Requests.Count.ShouldBe(1);
    }

    /// <summary>Sin reintentos, se abre tras dos fallos y se cierra a prueba en un segundo.</summary>
    private static LlmResilienceOptions Breaker() => new()
    {
        MaxRetryAttempts = 0,
        FailureRatio = 1,
        MinimumThroughput = 2,
        SamplingDurationSeconds = 60,
        BreakDurationSeconds = 1,
    };

    private static ServiceProvider Services(FakeHttpMessageHandler primaryHandler, LlmResilienceOptions resilience) =>
        LlmRegistrationTests.BuildServices(
            LlmRegistrationTests.Options(LlmProviderType.OpenAICompatible),
            _ => primaryHandler,
            resilience: resilience);

    private static Task<Application.Common.Result<LlmResponse<PingReply>>> CallAsync(ILlmProvider provider) =>
        provider.CompleteStructuredAsync<PingReply>(LlmRegistrationTests.Request(), TestContext.Current.CancellationToken);
}
