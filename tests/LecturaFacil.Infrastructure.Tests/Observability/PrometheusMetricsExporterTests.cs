// SPDX-License-Identifier: Apache-2.0
using System.Diagnostics.Metrics;
using LecturaFacil.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace LecturaFacil.Infrastructure.Tests.Observability;

public sealed class PrometheusMetricsExporterTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();

    private IMeterFactory MeterFactory => _services.GetRequiredService<IMeterFactory>();

    public void Dispose() => _services.Dispose();

    [Fact]
    public void Render_WritesHelpTypeAndEscapedLabels()
    {
        using var exporter = new PrometheusMetricsExporter(MeterFactory);
        var counter = MeterFactory.Create("LecturaFacil.Pruebas").CreateCounter<long>("lecturafacil.pruebas", description: "Contador de prueba.");

        counter.Add(3, new KeyValuePair<string, object?>("motivo", "con \"comillas\"\ny salto"));

        exporter.Render().ShouldBe(
            "# HELP lecturafacil_pruebas_total Contador de prueba.\n" +
            "# TYPE lecturafacil_pruebas_total counter\n" +
            "lecturafacil_pruebas_total{motivo=\"con \\\"comillas\\\"\\ny salto\"} 3\n");
    }

    [Fact]
    public void Render_IgnoresMetersOfOtherLibrariesAndOfOtherContainers()
    {
        using var exporter = new PrometheusMetricsExporter(MeterFactory);
        using var otherContainer = new ServiceCollection().AddMetrics().BuildServiceProvider();
        using var foreign = new Meter("System.Net.Http");
        foreign.CreateCounter<long>("http.requests").Add(1);
        otherContainer.GetRequiredService<IMeterFactory>().Create("LecturaFacil.Otro").CreateCounter<long>("lecturafacil.otro").Add(1);

        exporter.Render().ShouldBeEmpty();
    }
}
