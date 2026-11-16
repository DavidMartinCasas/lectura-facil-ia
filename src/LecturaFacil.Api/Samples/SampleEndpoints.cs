// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Samples;

namespace LecturaFacil.Api.Samples;

/// <summary>
/// <c>GET /api/samples</c> (US-16): los textos de ejemplo para probar la herramienta. Exige sesión. Lanzar la adaptación
/// de un ejemplo es un envío normal a <c>POST /api/documents</c>, con sus límites y su desafío anti-bots.
/// </summary>
internal static class SampleEndpoints
{
    public const string Path = "/api/samples";

    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, (ISampleTextCatalog catalog) =>
                TypedResults.Ok(catalog.All().Select(SampleTextResponse.From).ToList()))
            .RequireAuthorization()
            .WithTags("Ejemplos")
            .WithName("ListSamples")
            .WithSummary("Textos de ejemplo, sin datos personales y con su fuente.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        return endpoints;
    }
}

internal sealed record SampleTextResponse(string Id, string Title, string Source, string Text)
{
    public static SampleTextResponse From(SampleText sample) => new(sample.Id, sample.Title, sample.Source, sample.Text);
}
