// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Export;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Exportación de la adaptación (US-14): <c>POST /api/documents/{id}/export?format=docx|pdf|md|txt</c>. Un documento
/// ajeno responde 404, igual que uno que no existe (OWASP API1 · BOLA). Solo traduce HTTP ↔ caso de uso: qué se exporta
/// lo decide <see cref="ExportDocument"/> y cómo, cada <see cref="IDocumentExporter"/>.
/// </summary>
internal static class DocumentExportEndpoints
{
    /// <summary>Generar un DOCX o un PDF cuesta CPU y memoria: la exportación tiene su propio límite de peticiones.</summary>
    public const string ExportRateLimitPolicy = "exports";

    private static readonly Dictionary<string, int> StatusCodesByError = new(StringComparer.Ordinal)
    {
        [DocumentErrors.NotFound.Code] = StatusCodes.Status404NotFound,
        [ExportErrors.UnsupportedFormat.Code] = StatusCodes.Status400BadRequest,
        [ExportErrors.PendingWarnings.Code] = StatusCodes.Status409Conflict,
        [ExportErrors.NothingToExport.Code] = StatusCodes.Status409Conflict,
    };

    public static RouteGroupBuilder MapDocumentExport(this RouteGroupBuilder group)
    {
        // Es un POST: pasa por la validación antiforgery de AntiforgeryMiddleware aunque no cambie estado.
        group.MapPost("/{id:guid}/export", ExportAsync)
            .RequireRateLimiting(ExportRateLimitPolicy)
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithName("ExportDocument")
            .WithSummary("Descarga la última versión adaptada en DOCX, PDF, Markdown o texto.")
            .WithDescription(
                "format admite solo docx, pdf, md o txt. El fichero lleva el aviso de borrador pendiente de validación y la "
                + "fecha. 409 si quedan avisos de datos clave sin marcar como revisados o aún no hay adaptación.");

        return group;
    }

    private static async Task<IResult> ExportAsync(
        Guid id,
        string? format,
        HttpResponse response,
        ExportDocument exportDocument,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ToProblem(DocumentErrors.NotFound);
        }

        var result = await exportDocument.ExecuteAsync(DocumentId.From(id), format ?? string.Empty, cancellationToken);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        // Es el documento de una persona: ni el navegador ni un proxy intermedio deben guardar una copia.
        response.Headers.CacheControl = "no-store";
        var file = result.Value;
        return TypedResults.File(file.Content, file.ContentType, file.FileName);
    }

    private static IResult ToProblem(Error error) =>
        TypedResults.Problem(
            statusCode: StatusCodesByError.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest),
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
