// SPDX-License-Identifier: Apache-2.0
using FluentValidation;
using LecturaFacil.Application.Common;
using LecturaFacil.Application.Documents;
using LecturaFacil.Application.Review;
using LecturaFacil.Application.Validation;
using LecturaFacil.Domain.Documents;

namespace LecturaFacil.Api.Documents;

/// <summary>
/// Validación humana de un documento. La validación la hacen personas del grupo destinatario fuera de la aplicación; aquí
/// la misma persona que adaptó el documento lo envía a validación, anota el resultado y publica la versión validada:
/// <list type="bullet">
/// <item><c>POST /api/documents/{id}/validation/send</c>: Borrador → Pendiente de validación.</item>
/// <item><c>POST /api/documents/{id}/validation</c>: anota el resultado; Pendiente → Validado, o → Borrador si pide cambios.</item>
/// <item><c>POST /api/documents/{id}/validation/withdraw</c>: Pendiente → Borrador, sin anotar nada.</item>
/// <item><c>POST /api/documents/{id}/publish</c>: Validado → Publicado.</item>
/// </list>
/// Ninguno llama a un modelo. Un documento ajeno responde 404, igual que uno que no existe (OWASP API1 · BOLA).
/// </summary>
internal static class DocumentValidationEndpoints
{
    private static readonly Dictionary<string, int> StatusCodesByError = new(StringComparer.Ordinal)
    {
        [DocumentErrors.NotFound.Code] = StatusCodes.Status404NotFound,
        [ReviewErrors.NotEditable.Code] = StatusCodes.Status409Conflict,
        [ReviewErrors.VersionConflict.Code] = StatusCodes.Status409Conflict,
        [ValidationErrors.PendingWarnings.Code] = StatusCodes.Status409Conflict,
        [ValidationErrors.NotPending.Code] = StatusCodes.Status409Conflict,
        [ValidationErrors.NotValidated.Code] = StatusCodes.Status409Conflict,
    };

    public static RouteGroupBuilder MapDocumentValidation(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/validation/send", SendAsync)
            .Accepts<AcknowledgeKeyFactRequest>("application/json")
            .Produces<DocumentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("SendToValidation")
            .WithSummary("Envía a validación la última versión de un borrador.")
            .WithDescription(
                "version es la versión que se ha revisado y debe ser la última. 409 si el documento no está en borrador, "
                + "si hay otra versión o si quedan avisos de datos clave sin revisar.");

        group.MapPost("/{id:guid}/validation", RecordAsync)
            .Accepts<RecordValidationRequest>("application/json")
            .Produces<DocumentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("RecordValidation")
            .WithSummary("Anota el resultado de la validación con el grupo destinatario.")
            .WithDescription(
                "Approved deja el documento validado; ChangesRequested lo devuelve a borrador para corregirlo. "
                + "409 si el documento no está pendiente de validación.");

        group.MapPost("/{id:guid}/validation/withdraw", WithdrawAsync)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("WithdrawFromValidation")
            .WithSummary("Retira el documento de la validación y lo devuelve a borrador, sin anotar ningún resultado.");

        group.MapPost("/{id:guid}/publish", PublishAsync)
            .Produces<DocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("PublishDocument")
            .WithSummary("Publica un documento validado: su última versión es la final.");

        return group;
    }

    private static async Task<IResult> SendAsync(
        Guid id,
        AcknowledgeKeyFactRequest request,
        IValidator<AcknowledgeKeyFactRequest> validator,
        SendToValidation sendToValidation,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ToProblem(DocumentErrors.NotFound);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var command = new SendToValidationCommand(DocumentId.From(id), request.Version!.Value);
        return ToResponse(await sendToValidation.ExecuteAsync(command, cancellationToken));
    }

    private static async Task<IResult> RecordAsync(
        Guid id,
        RecordValidationRequest request,
        IValidator<RecordValidationRequest> validator,
        RecordValidation recordValidation,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ToProblem(DocumentErrors.NotFound);
        }

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return TypedResults.ValidationProblem(validation.ToDictionary());
        }

        var command = new RecordValidationCommand(
            DocumentId.From(id),
            request.Version!.Value,
            Enum.Parse<ValidationOutcome>(request.Outcome!),
            request.ValidatedBy!,
            request.Notes);
        return ToResponse(await recordValidation.ExecuteAsync(command, cancellationToken));
    }

    private static async Task<IResult> WithdrawAsync(Guid id, WithdrawFromValidation withdraw, CancellationToken cancellationToken) =>
        id == Guid.Empty
            ? ToProblem(DocumentErrors.NotFound)
            : ToResponse(await withdraw.ExecuteAsync(DocumentId.From(id), cancellationToken));

    private static async Task<IResult> PublishAsync(Guid id, PublishDocument publish, CancellationToken cancellationToken) =>
        id == Guid.Empty
            ? ToProblem(DocumentErrors.NotFound)
            : ToResponse(await publish.ExecuteAsync(DocumentId.From(id), cancellationToken));

    private static IResult ToResponse(Result<DocumentDetails> result) =>
        result.IsSuccess ? TypedResults.Ok(DocumentResponse.From(result.Value)) : ToProblem(result.Error!);

    private static IResult ToProblem(Error error) =>
        TypedResults.Problem(
            statusCode: StatusCodesByError.GetValueOrDefault(error.Code, StatusCodes.Status400BadRequest),
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
