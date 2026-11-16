// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Adaptation;
using LecturaFacil.Application.Llm;
using LecturaFacil.Domain.Documents;
using LecturaFacil.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LecturaFacil.Infrastructure.Tests.Observability;

public sealed class AdaptationTelemetryTests : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly CapturingLogger<AdaptationTelemetry> _logger = new();

    public AdaptationTelemetryTests()
    {
        _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        Exporter = new PrometheusMetricsExporter(MeterFactory);
        Telemetry = new AdaptationTelemetry(MeterFactory, _logger);
    }

    private System.Diagnostics.Metrics.IMeterFactory MeterFactory => _services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();

    private PrometheusMetricsExporter Exporter { get; }

    private AdaptationTelemetry Telemetry { get; }

    public void Dispose()
    {
        Exporter.Dispose();
        _services.Dispose();
    }

    [Fact]
    public void Record_WhenTheAdaptationSucceeds_LogsOneEventWithAllTheAdaptationFields()
    {
        var id = DocumentId.New();

        Telemetry.Record(Success(id, LlmProviderRole.Fallback));

        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.EventName.ShouldBe("AdaptationCompleted");
        entry.Properties["DocumentId"].ShouldBe(id.Value);
        entry.Properties["ProviderRole"].ShouldBe("fallback");
        entry.Properties["Provider"].ShouldBe("Nube");
        entry.Properties["Model"].ShouldBe("modelo-x");
        entry.Properties["InputTokens"].ShouldBe(1200L);
        entry.Properties["OutputTokens"].ShouldBe(900L);
        entry.Properties["DurationMs"].ShouldBe(2500L);
        entry.Properties["Result"].ShouldBe(AdaptationOutcome.SuccessResult);
    }

    [Fact]
    public void Record_WhenTheAdaptationFails_LogsAWarningWithTheErrorCodeAndNoProvider()
    {
        Telemetry.Record(new AdaptationOutcome(DocumentId.New(), null, null, null, 0, 0, TimeSpan.FromSeconds(1), LlmErrors.ProviderUnavailable.Code));

        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.EventName.ShouldBe("AdaptationFailed");
        entry.Properties["ProviderRole"].ShouldBe(AdaptationTelemetry.NoProvider);
        entry.Properties["Result"].ShouldBe(LlmErrors.ProviderUnavailable.Code);
    }

    [Fact]
    public void Record_CountsAdaptationsByProviderRoleAndResult_AndErrorsByCode()
    {
        Telemetry.Record(Success(DocumentId.New(), LlmProviderRole.Primary));
        Telemetry.Record(Success(DocumentId.New(), LlmProviderRole.Primary));
        Telemetry.Record(Success(DocumentId.New(), LlmProviderRole.Fallback));
        Telemetry.Record(AdaptationOutcome.Unexpected(DocumentId.New(), TimeSpan.FromSeconds(3)));

        var metrics = Exporter.Render();

        metrics.ShouldContain("""lecturafacil_adaptations_total{provider_role="primary",result="success"} 2""");
        metrics.ShouldContain("""lecturafacil_adaptations_total{provider_role="fallback",result="success"} 1""");
        metrics.ShouldContain("""lecturafacil_adaptations_total{provider_role="none",result="failure"} 1""");
        metrics.ShouldContain("""lecturafacil_adaptation_errors_total{error="unexpected"} 1""");
        metrics.ShouldContain("""lecturafacil_adaptation_tokens_total{direction="input",provider_role="primary"} 2400""");
        metrics.ShouldContain("""lecturafacil_adaptation_duration_seconds_count{provider_role="primary",result="success"} 2""");
        metrics.ShouldContain("""lecturafacil_adaptation_duration_seconds_bucket{provider_role="primary",result="success",le="2.5"} 2""");
        metrics.ShouldContain("""lecturafacil_adaptation_duration_seconds_bucket{provider_role="none",result="failure",le="2.5"} 0""");
        metrics.ShouldContain("""lecturafacil_adaptation_duration_seconds_bucket{provider_role="none",result="failure",le="+Inf"} 1""");
        metrics.ShouldNotContain("DocumentId", Case.Insensitive);
    }

    private static AdaptationOutcome Success(DocumentId id, LlmProviderRole role) =>
        new(id, role, role == LlmProviderRole.Primary ? "Servidor propio" : "Nube", "modelo-x", 1200, 900, TimeSpan.FromMilliseconds(2500), AdaptationOutcome.SuccessResult);
}
