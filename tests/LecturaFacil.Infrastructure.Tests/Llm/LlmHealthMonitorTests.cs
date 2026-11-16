// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class LlmHealthMonitorTests
{
    [Fact]
    public async Task Probe_WhenPrimaryDoesNotRespond_MarksItUnavailable()
    {
        var availability = new LlmAvailability(fallbackEnabled: false);
        var monitor = Monitor(new LlmProviders(FakeLlmProvider.Unavailable(), null), availability);

        await monitor.ProbeAsync(TestContext.Current.CancellationToken);

        availability.Current.ShouldBe(new LlmAvailabilityStatus(PrimaryAvailable: false, FallbackAvailable: null));
    }

    [Fact]
    public async Task Probe_WhenPrimaryRespondsAgain_MarksItAvailable()
    {
        var up = false;
        var primary = new FakeLlmProvider("Principal", _ => up ? LlmRecording.Load("ping/ok") : LlmRecording.Unavailable);
        var availability = new LlmAvailability(fallbackEnabled: false);
        var monitor = Monitor(new LlmProviders(primary, null), availability);
        await monitor.ProbeAsync(TestContext.Current.CancellationToken);

        up = true;
        await monitor.ProbeAsync(TestContext.Current.CancellationToken);

        availability.Current.PrimaryAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Probe_WithFallback_ChecksEachProviderWithoutSendingAnyDocument()
    {
        var primary = FakeLlmProvider.FromFixture("ping/ok", "Principal");
        var fallback = FakeLlmProvider.Unavailable("Respaldo");
        var availability = new LlmAvailability(fallbackEnabled: true);
        var monitor = Monitor(new LlmProviders(primary, fallback), availability);

        await monitor.ProbeAsync(TestContext.Current.CancellationToken);

        availability.Current.ShouldBe(new LlmAvailabilityStatus(PrimaryAvailable: true, FallbackAvailable: false));
        primary.Requests.ShouldHaveSingleItem().UserMessage.ShouldBe("ping");
        fallback.Requests.ShouldHaveSingleItem().UserMessage.ShouldBe("ping");
    }

    [Fact]
    public async Task Run_WhenHealthCheckIsDisabled_NeverCallsTheProviders()
    {
        var primary = FakeLlmProvider.FromFixture("ping/ok");
        using var monitor = Monitor(
            new LlmProviders(primary, null),
            new LlmAvailability(fallbackEnabled: false),
            new LlmResilienceOptions { HealthCheckEnabled = false });

        await monitor.StartAsync(TestContext.Current.CancellationToken);
        await monitor.StopAsync(TestContext.Current.CancellationToken);

        primary.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_WhenTheCheckThrowsUnexpectedly_KeepsRunningWithTheLastKnownState()
    {
        var probed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var availability = new LlmAvailability(fallbackEnabled: false);
        using var monitor = new LlmHealthMonitor(
            () =>
            {
                probed.TrySetResult();
                throw new InvalidOperationException("fallo inesperado");
            },
            new EmbeddedPromptLoader(),
            availability,
            Options.Create(new LlmResilienceOptions()),
            TimeProvider.System,
            NullLogger<LlmHealthMonitor>.Instance);

        await monitor.StartAsync(TestContext.Current.CancellationToken);
        await probed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        monitor.ExecuteTask!.IsFaulted.ShouldBeFalse();
        availability.Current.PrimaryAvailable.ShouldBeTrue();
        await monitor.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Availability_BeforeTheFirstCheck_AssumesTheConfiguredProvidersAreAvailable()
    {
        new LlmAvailability(fallbackEnabled: true).Current.ShouldBe(new LlmAvailabilityStatus(true, true));
        new LlmAvailability(fallbackEnabled: false).Current.ShouldBe(new LlmAvailabilityStatus(true, null));
    }

    [Theory]
    [InlineData(true, HealthStatus.Healthy)]
    [InlineData(false, HealthStatus.Degraded)]
    public async Task HealthCheck_ReflectsThePrimaryAvailability(bool primaryAvailable, HealthStatus expected)
    {
        var availability = new LlmAvailability(fallbackEnabled: false);
        availability.Update(new LlmAvailabilityStatus(primaryAvailable, null));
        var check = new LlmHealthCheck(availability);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("llm", check, HealthStatus.Degraded, null),
        };

        var result = await check.CheckHealthAsync(context, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(expected);
        result.Description.ShouldBeNull();
    }

    private static LlmHealthMonitor Monitor(
        LlmProviders providers,
        LlmAvailability availability,
        LlmResilienceOptions? options = null) =>
        new(
            () => providers,
            new EmbeddedPromptLoader(),
            availability,
            Options.Create(options ?? new LlmResilienceOptions()),
            TimeProvider.System,
            NullLogger<LlmHealthMonitor>.Instance);
}
