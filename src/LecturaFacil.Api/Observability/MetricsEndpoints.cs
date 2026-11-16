// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Infrastructure.Observability;

namespace LecturaFacil.Api.Observability;

internal static class MetricsEndpoints
{
    public const string Path = "/metrics";

    /// <summary>
    /// Métricas en formato Prometheus, solo para la red interna (<see cref="InternalNetwork"/>): desde fuera responde
    /// 404, como si no existiera. Además, el nginx público no la reenvía. No lleva datos de documentos ni de personas.
    /// </summary>
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Se crea ya, al arrancar: el exportador solo cuenta lo que se mide después de crearse.
        var exporter = endpoints.ServiceProvider.GetRequiredService<PrometheusMetricsExporter>();
        endpoints.MapGet(Path, (HttpContext context) => InternalNetwork.IsInternal(context)
                ? Results.Text(exporter.Render(), PrometheusMetricsExporter.ContentType)
                : Results.NotFound())
            .AllowAnonymous()
            .ExcludeFromDescription();
        return endpoints;
    }
}
