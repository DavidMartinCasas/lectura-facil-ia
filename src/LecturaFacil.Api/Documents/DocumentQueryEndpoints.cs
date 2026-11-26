// SPDX-License-Identifier: Apache-2.0
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Domain.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// <c>GET /api/documents</c> (lista del usuario, paginada), <c>GET /api/documents/{id}</c> y <c>GET /api/documents/{id}/events</c> (SSE). Un documento que no existe y uno de otro
/// usuario responden igual, 404, para no revelar que existe (OWASP API1 · BOLA).
/// </summary>
internal static class DocumentQueryEndpoints
{
    public const string StatusEventType = "status";

    /// <summary>Evento sin datos que mantiene viva la conexión; el navegador solo escucha <see cref="StatusEventType"/>.</summary>
    public const string PingEventType = "ping";

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

    private static async Task<IResult> EventsAsync(
        Guid id,
        WatchDocument watchDocument,
        IOptions<LimitsOptions> limits,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var result = await watchDocument.StartAsync(DocumentId.From(id), cancellationToken);
        if (!result.IsSuccess)
        {
            return ToProblem(result.Error!);
        }

        // Un nginx delante (por ejemplo, el de un proxy inverso) no guarda los eventos en su búfer: llegan al momento.
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        var heartbeat = TimeSpan.FromSeconds(limits.Value.DocumentEventsHeartbeatSeconds);
        return TypedResults.ServerSentEvents(ToEvents(id, result.Value, heartbeat, timeProvider, cancellationToken));
    }

    /// <summary>Cada cambio como evento <c>status</c> y, si pasa <paramref name="heartbeat"/> sin cambios, un <c>ping</c>.</summary>
    private static async IAsyncEnumerable<SseItem<DocumentStatusEvent?>> ToEvents(
        Guid id,
        IAsyncEnumerable<DocumentProgress> progress,
        TimeSpan heartbeat,
        TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var changes = progress.GetAsyncEnumerator(cancellationToken);
        var next = changes.MoveNextAsync().AsTask();
        try
        {
            while (true)
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var ping = Task.Delay(heartbeat, timeProvider, wait.Token);
                if (await Task.WhenAny(next, ping) == ping)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return new SseItem<DocumentStatusEvent?>(null, PingEventType);
                    continue;
                }

                await wait.CancelAsync();
                if (!await next)
                {
                    yield break;
                }

                var (status, stage, part, parts) = changes.Current;
                yield return new SseItem<DocumentStatusEvent?>(
                    new DocumentStatusEvent(id, status.ToString(), stage?.ToString(), part, parts),
                    StatusEventType);
                next = changes.MoveNextAsync().AsTask();
            }
        }
        finally
        {
            // No se libera el enumerador con una lectura pendiente: se espera a que la cancelación la termine.
            try
            {
                await next;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static IResult NotFound() => ToProblem(DocumentErrors.NotFound);

    private static IResult ToProblem(Error error) =>
        TypedResults.Problem(
            statusCode: error == DocumentErrors.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
