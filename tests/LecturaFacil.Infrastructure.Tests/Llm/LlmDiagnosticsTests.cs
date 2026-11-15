// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Llm;
using LecturaFacil.Infrastructure.Llm;
using LecturaFacil.Testing;

namespace LecturaFacil.Infrastructure.Tests.Llm;

public sealed class LlmDiagnosticsTests
{
    [Fact]
    public async Task PingAll_CallsEveryConfiguredProviderOncePerRole()
    {
        var primary = FakeLlmProvider.FromFixture("ping/ok", "Principal");
        var fallback = FakeLlmProvider.FromFixture("ping/ok", "Respaldo");
        var diagnostics = new LlmDiagnostics(new LlmProviders(primary, fallback), new EmbeddedPromptLoader());

        var results = await diagnostics.PingAllAsync(TestContext.Current.CancellationToken);

        results.Select(result => (result.Provider, result.Role)).ShouldBe(
        [
            ("Principal", LlmRole.Adapter),
            ("Principal", LlmRole.Verifier),
            ("Respaldo", LlmRole.Adapter),
            ("Respaldo", LlmRole.Verifier),
        ]);
        results.ShouldAllBe(result => result.Success && result.Model == "modelo-falso" && result.Error == null);
        results[0].InputTokens.ShouldBe(42);
        results[0].DurationMs.ShouldBe(120);
        primary.Requests.ShouldAllBe(request => request.MaxOutputTokens <= 64);
    }

    [Fact]
    public async Task PingAll_WithoutFallback_OnlyCallsThePrimary()
    {
        var primary = FakeLlmProvider.FromFixture("ping/ok");
        var diagnostics = new LlmDiagnostics(new LlmProviders(primary, null), new EmbeddedPromptLoader());

        var results = await diagnostics.PingAllAsync(TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        primary.Requests.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("ping/sin-campo", "llm.invalid_provider_output")]
    [InlineData("ping/truncado", "llm.invalid_provider_output")]
    public async Task PingAll_WhenTheReplyDoesNotMatchTheSchema_ReportsTheErrorCode(string fixture, string error)
    {
        var diagnostics = new LlmDiagnostics(
            new LlmProviders(FakeLlmProvider.FromFixture(fixture, "Principal"), null),
            new EmbeddedPromptLoader());

        var results = await diagnostics.PingAllAsync(TestContext.Current.CancellationToken);

        results.ShouldAllBe(result => !result.Success && result.Error == error && result.Provider == "Principal" && result.Model == null);
    }

    [Fact]
    public async Task PingAll_WhenAProviderIsDown_ReportsItAndStillPingsTheOthers()
    {
        var fallback = FakeLlmProvider.FromFixture("ping/ok", "Respaldo");
        var diagnostics = new LlmDiagnostics(
            new LlmProviders(FakeLlmProvider.Unavailable("Principal"), fallback),
            new EmbeddedPromptLoader());

        var results = await diagnostics.PingAllAsync(TestContext.Current.CancellationToken);

        results.Where(result => result.Provider == "Principal").ShouldAllBe(result => result.Error == "llm.provider_unavailable");
        results.Where(result => result.Provider == "Respaldo").ShouldAllBe(result => result.Success);
    }
}
