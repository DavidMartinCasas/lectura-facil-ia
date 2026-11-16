// SPDX-License-Identifier: Apache-2.0
using System.Runtime.CompilerServices;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// <c>GET /api/documents</c> (lista del usuario), <c>GET /api/documents/{id}</c> y <c>GET /api/documents/{id}/events</c> (SSE). Un documento que no existe y uno de otro
/// usuario responden igual, 404, para no revelar que existe (OWASP API1 · BOLA).
/// </summary>
internal static class DocumentQueryEndpoints
{
    public const string StatusEventType = "status";

    public static RouteGroupBuilder MapDocumentQueries(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .Produces<IReadOnlyList<DocumentListItemResponse>>()
            .WithName("ListDocuments")
            .WithSummary("Documentos del usuario, del más nuevo al más antiguo: título, fecha, estado y proveedor.");

        group.MapGet("/{id:guid}", GetAsync)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetDocument")
            .WithSummary("Estado de un documento y, si está en Draft, su adaptación (borrador pendiente de validación humana).");

        group.MapGet("/{id:guid}/events", EventsAsync)
            .Produces<DocumentStatusEvent>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetDocumentEvents")
            .WithSummary("Cambios de estado del documento por Server-Sent Events, hasta que termina la adaptación.");

        return group;
    }

    private static async Task<IResult> ListAsync(ListDocuments listDocuments, CancellationToken cancellationToken)
    {
        var documents = await listDocuments.ExecuteAsync(cancellationToken);
        return TypedResults.Ok(documents.Select(DocumentListItemResponse.From).ToList());
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
        IAsyncEnumerable<DocumentStatus> statuses,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var status in statuses.WithCancellation(cancellationToken))
        {
            yield return new DocumentStatusEvent(id, status.ToString());
        }
    }

    private static IResult NotFound() => ToProblem(DocumentErrors.NotFound);

    private static IResult ToProblem(Error error) =>
        TypedResults.Problem(
            statusCode: error == DocumentErrors.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
