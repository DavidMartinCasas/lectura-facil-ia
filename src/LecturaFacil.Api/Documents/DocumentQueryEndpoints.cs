// SPDX-License-Identifier: Apache-2.0
using System.Runtime.CompilerServices;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using Microsoft.AspNetCore.Mvc;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// <c>GET /api/documents</c> (lista del usuario, paginada), <c>GET /api/documents/{id}</c> y <c>GET /api/documents/{id}/events</c> (SSE). Un documento que no existe y uno de otro
/// usuario responden igual, 404, para no revelar que existe (OWASP API1 · BOLA).
/// </summary>
internal static class DocumentQueryEndpoints
{
    public const string StatusEventType = "status";

    public static RouteGroupBuilder MapDocumentQueries(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .Produces<DocumentListResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("ListDocuments")
            .WithSummary("Documentos del usuario, del más nuevo al más antiguo, por páginas y con filtro por título y estado.");

        group.MapGet("/{id:guid}", GetAsync)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetDocument")
            .WithSummary("Estado de un documento y, si está en Draft, su adaptación (borrador pendiente de validación humana).");

        group.MapGet("/{id:guid}/events", EventsAsync)
            .Produces<DocumentStatusEvent>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetDocumentEvents")
            .WithSummary("Cambios de estado (y de fase, mientras se adapta) del documento por Server-Sent Events, hasta que termina la adaptación.");

        return group;
    }

    /// <param name="title">Texto que debe aparecer en el título, sin distinguir mayúsculas ni tildes.</param>
    /// <param name="status">Estados admitidos; se puede repetir (<c>?status=Draft&amp;status=Validated</c>).</param>
    /// <param name="cursor">El <c>nextCursor</c> de la página anterior.</param>
    /// <param name="limit">Documentos por página.</param>
    private static async Task<IResult> ListAsync(
        ListDocuments listDocuments,
        [FromQuery] string? title,
        [FromQuery] string[]? status,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var result = await listDocuments.ExecuteAsync(new DocumentListRequest(title, status, cursor, limit), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(DocumentListResponse.From(result.Value)) : ToProblem(result.Error!);
    }

    private static async Task<IResult> GetAsync(Guid id, GetDocument getDocument, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var result = await getDocument.ExecuteAsync(DocumentId.From(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(DocumentResponse.From(result.Value)) : ToProblem(result.Error!);
    }

    private static async Task<IResult> EventsAsync(Guid id, WatchDocument watchDocument, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var result = await watchDocument.StartAsync(DocumentId.From(id), cancellationToken);
        return result.IsSuccess
            ? TypedResults.ServerSentEvents(ToEvents(id, result.Value, cancellationToken), StatusEventType)
            : ToProblem(result.Error!);
    }

    private static async IAsyncEnumerable<DocumentStatusEvent> ToEvents(
        Guid id,
        IAsyncEnumerable<DocumentProgress> progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var (status, stage) in progress.WithCancellation(cancellationToken))
        {
            yield return new DocumentStatusEvent(id, status.ToString(), stage?.ToString());
        }
    }

    private static IResult NotFound() => ToProblem(DocumentErrors.NotFound);

    private static IResult ToProblem(Error error) =>
        TypedResults.Problem(
            statusCode: error == DocumentErrors.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
